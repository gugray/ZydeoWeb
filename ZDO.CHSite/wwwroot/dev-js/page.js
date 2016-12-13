/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/history.min.js" />
/// <reference path="auth.js" />

var uiStrings = uiStringsEn;

function startsWith(str, prefix) {
  if (str.length < prefix.length)
    return false;
  for (var i = prefix.length - 1; (i >= 0) && (str[i] === prefix[i]) ; --i)
    continue;
  return i < 0;
}

function escapeHTML(s) {
  return s.replace(/&/g, '&amp;')
          .replace(/"/g, '&quot;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;');
}

var zdPage = (function () {
  "use strict";

  var reqId = 0; // Current page load request ID. If page has moved on, earlier requests ignored when they complete.
  var location = null; // Full location, as seen in navbar
  var path = null; // Path after domain name
  var lang = null; // Language (first section of path)
  var rel = null; // Relative path (path without language ID at start)

  // Page init scripts for each page (identified by relPath).
  var initScripts = {};
  // Global init scripts invoked on documentReady.
  var globalInitScripts = [];

  // Function that provides search parameters (submitted alongside regular AJAX page requests).
  var searchParamsProvider = null;

  // Close function of currently active modal popup, or null.
  var activeModalCloser = null;

  // Incremented for subsequent alerts, so we can correctly animate new one shown before old one has expired.
  var alertId = 0;
 
  // Parse full path, language, and relative path from URL
  function parseLocation() {
    location = window.history.location || window.location;
    var rePath = /https?:\/\/[^\/]+(.*)/i;
    var match = rePath.exec(location);
    path = match[1];
    if (startsWith(path, "/en/") || path == "/en") {
      lang = "en";
      rel = path == "/en" ? "" : path.substring(4);
      uiStrings = uiStringsEn;
    }
    else if (startsWith(path, "/hu/") || path == "/hu") {
      lang = "hu";
      rel = path == "/hu" ? "" : path.substring(4);
      uiStrings = uiStringsHu;
    }
    else if (startsWith(path, "/de/") || path == "/de") {
      lang = "de";
      rel = path == "/de" ? "" : path.substring(4);
      uiStrings = uiStringsDe;
    }
    else {
      lang = "en";
      rel = path;
    }
  }

  // Page just loaded: time to get dynamic part asynchronously, wherever we just landed
  $(document).ready(function () {
    // Make sense of location
    parseLocation();
    // Adapt font size to window width
    $(window).resize(onResize);
    onResize();
    // Update menu to show where I am (will soon end up being)
    updateMenuState();
    zdAuth.initOnLoad(loginChanged);
    // Cookie warning, Imprint link, login/logout command etc.
    initGui();
    // Global script initializers
    for (var i = 0; i != globalInitScripts.length; ++i) globalInitScripts[i]();
    // Request dynamic page - async
    // Skipped if we received page with content present already
    var hasContent = $("body").hasClass("has-initial-content");
    if (!hasContent) {
      ++reqId;
      var id = reqId;
      var data = { lang: lang, rel: rel, isMobile: zdPage.isMobile() };
      // Infuse extra params (search)
      infuseSearchParams(data);
      // Submit request
      var req = zdAuth.ajax("/api/dynpage/getpage", "GET", data);
      req.done(function (data) {
        dynReady(data, id);
      });
      req.fail(function (jqXHR, textStatus, error) {
        doApplyFailHtml();
      });
      // Generic click-away handler to close active popup, and also hamburger menu
      $('html').click(function () {
        if (activeModalCloser != null) {
          activeModalCloser();
          activeModalCloser = null;
        }
        $("#headerStickHamburger").removeClass("openBurger");
      });
    }
    // If page has initial content, trigger dyn-content-loaded activities right now
    if (hasContent) dynReady(null, -1);
  });

  // Infuses additional parameters to be submitted in search requests.
  function infuseSearchParams(data) {
    if (!startsWith(data.rel, "search/")) return;
    var params = searchParamsProvider();
    for (var fld in params) data[fld] = params[fld];
  }

  // Measure m width against viewport; adapt font size
  function onResize() {
    var ww = window.innerWidth;
    var w10em = $("#emMeasure")[0].clientWidth;
    var ratio = ww / w10em;
    // For diagnostics
    //var dynWidth = $("#dynPage").width();
    //$("#debug").text("Win: " + ww + " MM: " + w10em + " DP: " + dynWidth + " R: " + ratio);

    // Clear previous styles
    $("html").removeClass("resplay-stickleft");
    $("html").removeClass("resplay-hamburger");
    $("html").removeClass("respfont-small");
    $(".txtSearch").removeClass("active");
    // Layout: stick left (not center) if small-ish; hamburger + full-width if real small
    if (ratio <= 7.8 && ratio > 5.8) $("html").addClass("resplay-stickleft");
    if (ratio <= 5.8) {
      $("html").addClass("resplay-hamburger");
      $("#headerStickHamburger .txtSearch").addClass("active");
      $('.tooltipstered').tooltipster("disable");
    }
    else $("#headerStickFull .txtSearch").addClass("active");
    // Font size: decrease a little within small-ish
    if (ratio < 7) $("html").addClass("respfont-small");
  }

  // Navigate within single-page app (invoked from link click handler)
  function dynNavigate() {
    // Make sense of location
    parseLocation();
    // Fade out current content to indicate navigation
    $("#dynPage").addClass("fading");
    // Update menu to show where I am (will soon end up being)
    updateMenuState();
    // Request dynamic page - async
    ++reqId;
    var id = reqId;
    var data = { lang: lang, rel: rel, isMobile: zdPage.isMobile() };
    // Infuse extra search parameters
    infuseSearchParams(data);
    // Submit request
    var req = zdAuth.ajax("/api/dynpage/getpage", "GET", data);
    req.done(function (data) {
      if (data) navReady(data, id);
      else doApplyFailHtml();
    });
    req.fail(function (jqXHR, textStatus, error) {
      doApplyFailHtml();
    });
  }

  // Show error content in dynamic area
  function doApplyFailHtml() {
    // Meta: clear all, except title
    if ($("body").hasClass("hdd")) $(document).attr("title", zdPage.ui("oops", "title-hdd"));
    else $(document).attr("title", zdPage.ui("oops", "title-chd"));
    $("meta[name = 'keywords']").attr("content", "");
    $("meta[name = 'description']").attr("content", "");
    $("meta[name = 'robots']").attr("content", "");
    // Content
    var html = zdSnippets["oops"];
    html = zdPage.localize("oops", html);
    $("#dynPage").html(html);
    $("#dynPage").removeClass("fading");
  }

  // Apply dynamic content: HTML body, title, description, keywords; possible other data
  function applyDynContent(data) {
    // Infuse all the metainfo
    $(document).attr("title", data.title);
    $("meta[name = 'keywords']").attr("content", data.keywords);
    $("meta[name = 'description']").attr("content", data.description);
    if (data.noIndex) $("meta[name = 'robots']").attr("content", "noindex,nofollow");
    else $("meta[name = 'robots']").attr("content", "");
    if (lang == "fan") $("html").attr("lang", "zh-TW");
    else if (lang == "jian") $("html").attr("lang", "zh-CN");
    else $("html").attr("lang", lang);
    // Actual page content
    $("#dynPage").html(data.html);
    $("#dynPage").removeClass("fading");
    $("#headerStickHamburger").removeClass("openBurger");
    // Run this page's script initializer, if any
    runInitScripts(data);
    // Scroll to top
    $(window).scrollTop(0);
  }

  // Initializes scripts that signed up for current page.
  function runInitScripts(data) {
    for (var key in initScripts) {
      if (startsWith(rel, key)) initScripts[key](data);
      // Hack: call search initializer for ""
      if (rel == "" && key == "search") initScripts[key](data);
    }
  }

  function navReady(data, id) {
    // An obsolete request completing too late?
    if (id != reqId) return;

    // Show dynamic content, title etc.
    applyDynContent(data);
    // Fix title in hamburger mode
    fixHamTitle();
    // GA single-page navigation
    ga('set', 'page', path);
    ga('send', {
      hitType: 'pageview',
      page: path,
      title: data.title
    });
  }

  // Dynamic data received after initial page load (not within single-page navigation)
  function dynReady(data, id) {
    // An obsolete request completing too late?
    if (id != -1 && id != reqId) return;

    // Show dynamic content, title etc.
    // Data is null if we're called directly from page load (content already present)
    // Otherwise, we still need to run init scripts
    if (data != null) applyDynContent(data);
    else runInitScripts(null);

    // Set up single-page navigation
    $(document).on('click', 'a.ajax', function () {
      // Navigation closes any active modal popup
      if (activeModalCloser != null) {
        activeModalCloser();
        activeModalCloser = null;
      }
      // Trick: If we're on search page but menu is shown, link just changes display; no navigation
      if ((rel == "" || startsWith(rel, "search")) && $(this).attr("id") == "topMenuSearch") {
        $(".hdrSearch").addClass("on");
        $(".hdrTitle").removeClass("on");
        $("#hdrMenu").removeClass("on");
        $("#subHeader").removeClass("visible");
        return false;
      }
      // Navigate
      history.pushState(null, null, this.href);
      dynNavigate();
      return false;
    });
    $(window).on('popstate', function (e) {
      dynNavigate();
    });

    // *NOW* that we're all done, show page.
    $("body").css("visibility", "visible");
    // Events - toggle from lookup input to menu
    $("#toMenu").click(function () {
      $(".hdrSearch").removeClass("on");
      $("#hdrMenu").addClass("on");
      //$("#subHeader").addClass("visible");
    });
    // Hamburger menu
    $("#burger").click(function (evt) {
      // Toggle hamburger menu
      if ($("#headerStickHamburger").hasClass("openBurger")) $("#headerStickHamburger").removeClass("openBurger");
      else $("#headerStickHamburger").addClass("openBurger");
      // Hide any modal popups
      if (activeModalCloser != null) activeModalCloser();
      activeModalCloser = null;
      // Stop propagating, or we'll self-close right away.
      evt.stopPropagation();
    });
  }

  // General UI event wireup
  function initGui() {
    // Cookie warning / opt-in pest
    var cookies = localStorage.getItem("cookies");
    if (cookies != "go") $("#bittercookie").css("display", "block");
    $("#swallowbitterpill").click(function (evt) {
      $("#bittercookie").css("display", "none");
      localStorage.setItem("cookies", "go");
      evt.preventDefault();
    });
    // Link to imprint
    $("#imprint").click(function () {
      window.open("/" + zdPage.getLang() + "/read/details/imprint");
    });
    // Login/logout
    $("#smUserLogInOut").click(function (evt) {
      if (!zdAuth.isLoggedIn()) zdAuth.showLogin();
      else zdAuth.logout();
      evt.stopPropagation();
    });
  }

  function loginChanged() {
    if ($(".private-content").length > 0) zdPage.reload();
    else updateMenuState();
  }

  // Updates top navigation menu to reflect where we are
  function updateMenuState() {
    if (zdAuth.isLoggedIn()) {
      $(".loginIcon").addClass("loggedin");
      $("#smUserProfile").addClass("visible");
      $("#smUserLogInOut span").text(zdPage.ui("login", "menuLogout"));
    }
    else {
      $(".loginIcon").removeClass("loggedin");
      $("#smUserProfile").removeClass("visible");
      $("#smUserLogInOut span").text(zdPage.ui("login", "menuLogin"));
    }

    $(".topMenu").removeClass("on");
    $(".subMenu").removeClass("visible");
    $(".loginIcon").removeClass("on");
    if (rel == "" || startsWith(rel, "search")) {
      $("#hdrMenu").removeClass("on");
      $("#subHeader").removeClass("visible");
      $("#dynPage").addClass("nosubmenu");
      $("#headermask").addClass("nosubmenu");
      $(".hdrSearch").addClass("on");
      $(".hdrTitle").removeClass("on");
    }
    else {
      $(".hdrSearch").removeClass("on");
      $(".hdrTitle").addClass("on");
      $("#hdrMenu").addClass("on");
      $("#subHeader").addClass("visible");
      $("#dynPage").removeClass("nosubmenu");
      $("#headermask").removeClass("nosubmenu");
      if (startsWith(rel, "edit")) {
        $("#topMenuEdit").addClass("on");
        $("#subMenuEdit").addClass("visible");
      }
      else if (startsWith(rel, "read")) {
        $("#topMenuRead").addClass("on");
        $("#subMenuRead").addClass("visible");
      }
      else if (startsWith(rel, "download")) {
        $("#topMenuDownload").addClass("on");
        $("#subMenuDownload").addClass("visible");
      }
      else if (startsWith(rel, "user")) {
        $(".loginIcon").addClass("on");
        $("#subMenuUser").addClass("visible");
      }
    }
    $(".subMenu span").removeClass("on");
    if (startsWith(rel, "edit/new")) $("#smEditNew").addClass("on");
    else if (startsWith(rel, "edit/history")) $("#smEditHistory").addClass("on");
    else if (startsWith(rel, "edit/existing")) $("#smEditExisting").addClass("on");
    else if (startsWith(rel, "read/search-tips")) $("#smReadSearchTips").addClass("on");
    else if (startsWith(rel, "read/faq")) $("#smReadFAQ").addClass("on");
    else if (startsWith(rel, "read/details")) $("#smReadDetails").addClass("on");
    else if (startsWith(rel, "download/dictionary")) $("#smDownloadDict").addClass("on");
    else if (startsWith(rel, "download/software")) $("#smDownloadSoft").addClass("on");
    else if (startsWith(rel, "download/license")) $("#smDownloadLic").addClass("on");
    else if (startsWith(rel, "user/users")) $("#smUserUsers").addClass("on");
    else if (startsWith(rel, "user/profile")) $("#smUserProfile").addClass("on");
    // Fix title in hamburger mode
    fixHamTitle();
    // Language selector
    $(".langSelDe").attr("href", "/de/" + rel);
    $(".langSelHu").attr("href", "/hu/" + rel);
    $(".langSelEn").attr("href", "/en/" + rel);
    $(".langSel").removeClass("on");
    if (lang == "en") $(".langSelEn").addClass("on");
    else if (lang == "hu") $(".langSelHu").addClass("on");
    else if (lang == "de") $(".langSelDe").addClass("on");
  }

  // Fix title in hamburger mode if there is no submenu
  function fixHamTitle() {
    // In hamburger mode, steal title from selected submenu; or page's ".page-title" element
    var hamTitle = $(".subMenu span.on").text();
    if (!hamTitle || hamTitle == "") hamTitle = $("#page-title").text();
    $(".hdrTitle").text(hamTitle);
  }

  // Closes a standard modal dialog (shown by us).
  function doCloseModal(id, onClosed) {
    $("#" + id).remove();
    zdPage.modalHidden();
    if (onClosed) onClosed();
  }

  return {
    // Called by page-specific controller scripts to register themselves in single-page app, when page is navigated to.
    registerInitScript: function(pageRel, init) {
      initScripts[pageRel] = init;
    },

    globalInit: function(init) {
      globalInitScripts.push(init);
    },

    getLang: function() {
      return lang;
    },

    isMobile: function() {
      return $("html").hasClass("resplay-hamburger");
    },

    reload: function() {
      history.pushState(null, null, path);
      dynNavigate();
    },

    // Navigates to provided relative URL (excluding language, not leading slash)
    navigate: function (newRel) {
      history.pushState(null, null, "/" + lang + "/" + newRel);
      dynNavigate();
    },

    applyFailHtml: function () { doApplyFailHtml(); },

    submitSearch: function(query) {
      history.pushState(null, null, "/" + lang + "/search/" + query);
      dynNavigate();
    },

    // Called by lookup.js's global initializer to name search params provider function.
    setSearchParamsProvider: function(providerFun) {
      searchParamsProvider = providerFun;
    },

    // Gets the current selection's bounding element (start), or null if page has no selection.
    getSelBoundElm: function () {
      var range, sel, container;
      if (document.selection) {
        range = document.selection.createRange();
        range.collapse(true);
        if (range.toString() == "") return null;
        return range.parentElement();
      } else {
        sel = window.getSelection();
        if (sel.toString() == "") return null;
        if (sel.getRangeAt) {
          if (sel.rangeCount > 0) {
            range = sel.getRangeAt(0);
          }
        } else {
          // Old WebKit
          range = document.createRange();
          range.setStart(sel.anchorNode, sel.anchorOffset);
          range.setEnd(sel.focusNode, sel.focusOffset);

          // Handle the case when the selection was selected backwards (from the end to the start in the document)
          if (range.collapsed !== sel.isCollapsed) {
            range.setStart(sel.focusNode, sel.focusOffset);
            range.setEnd(sel.anchorNode, sel.anchorOffset);
          }
        }
        if (range) {
          container = range["startContainer"];
          // Check if the container is a text node and return its parent if so
          return container.nodeType === 3 ? container.parentNode : container;
        }
      }
    },

    // Called by any code showing a modal popup. Closes any active popup, and remembers close function.
    modalShown: function (closeFun) {
      if (activeModalCloser == closeFun) return;
      if (activeModalCloser != null) activeModalCloser();
      activeModalCloser = closeFun;
      // Close hamburger menu too
      $("#headerStickHamburger").removeClass("openBurger");
    },

    // Called by code when it closes modal of its own accord.
    modalHidden: function() {
      activeModalCloser = null;
    },

    // Shows a standard modal dialog with the provided content and callbacks.
    showModal: function (params) {
      // Close any other popup
      if (activeModalCloser != null) activeModalCloser();
      activeModalCloser = null;
      // Close hamburger menu too
      $("#headerStickHamburger").removeClass("openBurger");
      // Build popup's HTML
      var html = zdSnippets["modalPopup"];
      html = html.replace("{{id}}", params.id);
      html = html.replace("{{title}}", escapeHTML(params.title));
      html = html.replace("{{body}}", params.body);
      html = html.replace("{{ok}}", zdPage.ui("dialog", "ok"));
      html = html.replace("{{cancel}}", zdPage.ui("dialog", "cancel"));
      $("body").append(html);
      // Wire up events
      activeModalCloser = function () { doCloseModal(params.id, params.onClosed); };
      $(".modalPopupInner2").click(function (evt) { evt.stopPropagation(); });
      $(".modalPopupClose").click(function () { doCloseModal(params.id, params.onClosed); });
      $(".modalPopupButtonCancel").click(function () { doCloseModal(params.id, params.onClosed); });
      $(".modalPopupButtonOK").click(function () {
        if ($(this).hasClass("disabled")) return;
        if (params.confirmed()) doCloseModal(params.id, params.onClosed);
      });
      // Focus requested field
      if (params.toFocus) $(params.toFocus).focus();
    },

    // Sets or clears modal dialog's "busy" aka "working" state: animation; OK button disabled
    setModalWorking: function(id, working) {
      // Go to busy mode
      if (working) {
        $(id + " .modalPopupButtonOK").addClass("disabled");
        $(id + " .modalPopupWorking").addClass("visible");
        var deg = 0;
        var inc = 10;
        var funRotate = function () {
          if (!$(id + " .modalPopupButtonOK").hasClass("disabled")) return;
          deg += inc;
          if (deg < 180) inc += 3;
          else inc -= 3;
          $(id + " .modalPopupWorking").css("transform", "rotate(" + deg + "deg)");
          setTimeout(funRotate, 50);
        };
        setTimeout(funRotate, 50);
      }
      // Leave busy mode
      else {
        $(id + " .modalPopupButtonOK").removeClass("disabled");
        $(id + " .modalPopupWorking").removeClass("visible");
      }
    },

    // Closes modal dialog with the provided ID. Does not invoke onClosed callback.
    closeModal: function (id) { doCloseModal(id); },

    // Shows an alert at the top of the page.
    showAlert: function (title, body, isError) {
      // Remove old alert
      $(".alertBar").remove();
      // Class for current one
      ++alertId;
      var currBarId = "alertbar" + alertId;
      var currAlertId = "alert" + alertId;
      var templ = zdSnippets["alert"].replace("alertBarId", currBarId);
      templ = templ.replace("alertId", currAlertId);
      var elm = $(templ);
      $("body").append(elm);
      $("#" + currAlertId + " .alertTitle").text(title);
      if (body) {
        $("#" + currAlertId + " .alertTitle").append("<br>");
        $("#" + currAlertId + " .alertBody").text(body);
      }
      if (isError) $("#" + currAlertId).addClass("alertFail");
      else $("#" + currAlertId).addClass("alertOK");

      $("#" + currAlertId + " .alertClose").click(function () {
        $("#" + currBarId).remove();
        //$("#" + currAlertId).addClass("hidden");
        //setTimeout(function () {
        //  $("#" + currBarId).remove();
        //}, 5000);
      });

      setTimeout(function () {
        $("#" + currAlertId).addClass("visible");
        setTimeout(function () {
          $("#" + currAlertId).addClass("hidden");
          setTimeout(function () {
            $("#" + currBarId).remove();
          }, 1000)
        }, 5000);
      }, 50);
    },

    // Replaces placeholders in HTML template with localized texts for current language.
    localize: function (prefix, tmpl) {
      var rex = /\{\{([^\}]+)\}\}/;
      while (true) {
        var match = rex.exec(tmpl);
        if (!match) break;
        //var txt = uiStrings[prefix][match[1]];
        var txt = zdPage.ui(prefix, match[1]);
        txt = escapeHTML(txt);
        tmpl = tmpl.replace(match[0], txt);
      }
      return tmpl;
    },

    // Returns a localized UI string, with safe fallback to English and placeholder.
    ui: function (prefix, id) {
      var x = uiStrings[prefix];
      if (!x) x = uiStringsEn[prefix];
      if (!x) return "{" + prefix + "." + id + "}";
      var str = x[id];
      if (!str) str = "{" + prefix + "." + id + "}";
      return str;
    },

    // Returns true if current device is recognized to have a touch screen.
    isTouch: function () {
      // http://stackoverflow.com/questions/4817029/whats-the-best-way-to-detect-a-touch-screen-device-using-javascript
      var res = false;
      // works on most browsers 
      try { res |= 'ontouchstart' in window; } catch (e) { }
      // works on IE10/11 and Surface
      try { res |= navigator.maxTouchPoints; } catch (e) { }
      return res;
    }

  };

})();
