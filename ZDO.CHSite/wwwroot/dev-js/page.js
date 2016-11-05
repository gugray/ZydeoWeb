/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="x-history.min.js" />

var uiStrings = uiStringsHu;

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
    }
    else if (startsWith(path, "/de/") || path == "/de") {
      lang = "de";
      rel = path == "/de" ? "" : path.substring(4);
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
    // Update menu to show where I am (will soon end up being)
    updateMenuState();
    // Global script initializers
    for (var i = 0; i != globalInitScripts.length; ++i) globalInitScripts[i]();
    // Request dynamic page - async
    // Skipped if we received page with content present already
    var hasContent = $("body").hasClass("has-initial-content");
    if (!hasContent) {
      ++reqId;
      var id = reqId;
      var data = { lang: lang, rel: rel };
      // Infuse extra params (search)
      infuseSearchParams(data);
      // Submit request
      var req = $.ajax({
        url: "/api/dynpage/getpage",
        type: "GET",
        contentType: "application/x-www-form-urlencoded; charset=UTF-8",
        data: data
      });
      req.done(function (data) {
        dynReady(data, id);
      });
      req.fail(function (jqXHR, textStatus, error) {
        applyFailHtml();
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
    // Adapt font size to window width
    $(window).resize(onResize);
    onResize();
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
    var data = { lang: lang, rel: rel };
    // Infuse extra search parameters
    infuseSearchParams(data);
    // Submit request
    var req = $.ajax({
      url: "/api/dynpage/getpage",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: data
    });
    req.done(function (data) {
      navReady(data, id);
    });
    req.fail(function (jqXHR, textStatus, error) {
      applyFailHtml();
    });
  }

  // Show error content in dynamic area
  function applyFailHtml() {
    $("#dynPage").html("Ouch.");
    // TO-DO: fail title; keywords; description
  }

  // Apply dynamic content: HTML body, title, description, keywords; possible other data
  function applyDynContent(data) {
    $(document).attr("title", data.title);
    $("meta[name = 'keywords']").attr("content", data.keywords);
    $("meta[name = 'description']").attr("content", data.description);
    $("#dynPage").html(data.html);
    $("#dynPage").removeClass("fading");
    $("#headerStickHamburger").removeClass("openBurger");
    // Run this page's script initializer, if any
    for (var key in initScripts) {
      if (startsWith(rel, key)) initScripts[key](data);
      // Hack: call search initializer for ""
      if (rel == "" && key == "search") initScripts[key](data);
    }
    // Scroll to top
    $(window).scrollTop(0);
  }

  function navReady(data, id) {
    // An obsolete request completing too late?
    if (id != reqId) return;

    // Show dynamic content, title etc.
    applyDynContent(data);
  }

  // Dynamic data received after initial page load (not within single-page navigation)
  function dynReady(data, id) {
    // An obsolete request completing too late?
    if (id != -1 && id != reqId) return;

    // Show dynamic content, title etc.
    // Data is null if we're call directly from page load (content already present)
    if (data != null) applyDynContent(data);

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

  // Updates top navigation menu to reflect where we are
  function updateMenuState() {
    $(".topMenu").removeClass("on");
    $(".subMenu").removeClass("visible");
    if (rel == "" || startsWith(rel, "search")) {
      $("#hdrMenu").removeClass("on");
      $("#subHeader").removeClass("visible");
      $("#dynPage").addClass("nosubmenu");
      $("#headermask").addClass("nosubmenu");
      $(".hdrSearch").addClass("on");
    }
    else {
      $(".hdrSearch").removeClass("on");
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
    }
    $(".subMenu span").removeClass("on");
    if (startsWith(rel, "edit/new")) $("#smEditNew").addClass("on");
    else if (startsWith(rel, "edit/history")) $("#smEditHistory").addClass("on");
    else if (startsWith(rel, "edit/existing")) $("#smEditExisting").addClass("on");
    else if (startsWith(rel, "read/about")) $("#smReadAbout").addClass("on");
    else if (startsWith(rel, "read/version-history")) $("#smReadVersionHistory").addClass("on");
    else if (startsWith(rel, "read/articles")) $("#smReadArticles").addClass("on");
    else if (startsWith(rel, "read/etc")) $("#smReadEtc").addClass("on");
    // Language selector
    $(".langSelDe").attr("href", "/de/" + rel);
    $(".langSelHu").attr("href", "/hu/" + rel);
    $(".langSelEn").attr("href", "/en/" + rel);
    $(".langSel").removeClass("on");
    if (lang == "en") $(".langSelEn").addClass("on");
    else if (lang == "hu") $(".langSelHu").addClass("on");
    else if (lang == "de") $(".langSelDe").addClass("on");
  }

  // Closes a standard modal dialog (shown by us).
  function doCloseModal(id) {
    $("#" + id).remove();
    zdPage.modalHidden();
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
      html = html.replace("{{ok}}", uiStrings["dialog"]["ok"]);
      html = html.replace("{{cancel}}", uiStrings["dialog"]["cancel"]);
      $("#dynPage").append(html);
      // Wire up events
      activeModalCloser = function () { doCloseModal(params.id); };
      $(".modalPopupInner2").click(function (evt) { evt.stopPropagation(); });
      $(".modalPopupClose").click(function () { doCloseModal(params.id); });
      $(".modalPopupButtonCancel").click(function () { doCloseModal(params.id); });
      $(".modalPopupButtonOK").click(function () {
        if (params.confirmed()) doCloseModal(params.id);
      });
      // Focus requested field
      if (params.toFocus) $(params.toFocus).focus();
    },

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
        var txt = uiStrings[prefix][match[1]];
        txt = escapeHTML(txt);
        tmpl = tmpl.replace(match[0], txt);
      }
      return tmpl;
    },

    // Returns true if current device is recognized to have a touch screen.
    isTouch: function () {
      // http://stackoverflow.com/questions/4817029/whats-the-best-way-to-detect-a-touch-screen-device-using-javascript
      return 'ontouchstart' in window        // works on most browsers 
          || navigator.maxTouchPoints;       // works on IE10/11 and Surface
    }

  };

})();
