/// <reference path="x-jquery-2.1.4.min.js" />
/// <reference path="x-jquery.color-2.1.2.min.js" />
/// <reference path="x-jquery.tooltipster.min.js" />
/// <reference path="strings-hu.js" />
/// <reference path="page.js" />
/// <reference path="handwriting.js" />
/// <reference path="strokeanim.js" />

var zdLookup = (function () {
  "use strict";

  var clrSel = "#ffe4cc"; // Same as background-color of .optionItem.selected in CSS
  var clrEmph = "#ffc898"; // Same as border-bottom of .optionHead in CSS
  var optScript = "both";
  var optTones = "pleco";
  // -1 if prefix search suggesions are not shown, and when the list is not being keyboard-navigated.
  // When list is being keyboard-navigated, index of current item.
  var prefixActiveIx = -1;
  // Prefix request ID, to ignore delayed/out-of-order responses.
  var prefixReqId = 0;
  // Do *not* trigger prefix query on text change: means the change is b/c we're navigating list, or inserting from list
  var prefixSuppressTrigger = false;
  // True if IME composition is in progress in the input field
  var isComposing = false;

  zdPage.globalInit(globalInit);

  $(document).ready(function () {
    zdPage.registerInitScript("search", resultEventWireup);
  });

  function globalInit() {
    // Register search params provider
    zdPage.setSearchParamsProvider(getSearchParams);

    // If session storage says we've already loaded strokes, append script right now
    // This will happen from browser cache, i.e., page load doesn't suffer
    if (sessionStorage.getItem("charDataLoaded")) {
      var elmStrokes = document.createElement('script');
      document.getElementsByTagName("head")[0].appendChild(elmStrokes);
      elmStrokes.setAttribute("type", "text/javascript");
      elmStrokes.setAttribute("src", "/prod-js/xcharacterdata.js");
    }
    // Add tooltips
    if (!zdPage.isMobile()) {
      $(".btnWrite").tooltipster({ content: $("<span>" + uiStrings["tooltip-btn-brush"] + "</span>") });
      $(".btnSettings").tooltipster({ content: $("<span>" + uiStrings["tooltip-btn-settings"] + "</span>") });
      $(".btnSearch").tooltipster({ content: $("<span>" + uiStrings["tooltip-btn-search"] + "</span>") });
    }
    // Clear button; settings
    $("#btn-clear").click(clearSearch);
    $(".btnSettings").click(showSettings);
    // Handwriting recognition
    $(".btnWrite").click(function (event) {
      if ($("#stroke-input").css("display") == "block") hideStrokeInput();
      else showStrokeInput(event);
    });
    // Debug: to work on strokes input
    //showStrokeInput();

    $(".btnSearch").click(submitSearch);
    $(".txtSearch").keyup(function (e) {
      if (e.keyCode == 13) {
        if (prefixActiveIx >= 0) onPrefixKeyUpDown(e, true);
        // Yes, this way, list insertion with enter immediately triggers lookup too.
        submitSearch();
        return false;
      }
    });
    $(".txtSearch").keydown(function (e) {
      onPrefixKeyUpDown(e, false);
    });
    $(".txtSearch").focus(txtSearchFocus);
    $(".txtSearch").on("input", function () { if (!prefixSuppressTrigger) prefixTrigger(); });
    $('.txtSearch').on('compositionstart', function (e) { isComposing = true; });
    $('.txtSearch').on('compositionend', function (e) { isComposing = false; });

    // Debug: to work on opening screen
    //$("#resultsHolder").css("display", "none");
    //$("#welcomeScreen").css("display", "block");
  }

  function resultEventWireup(data) {
    $("#results").append("<div id='soaBox' class='soaBoxLeft'></div>");
    zdStrokeAnim.init();
    $(".hanim").click(showStrokeAnim);
    $("#soaClose").click(hideStrokeAnim);
    $("#soaBox").click(function (e) { e.stopPropagation(); });
    $('.txtSearch.active').val(data.data);
    // Hack [?] - but either something steals focus on load, or input field is not yet shown to accept focus.
    setTimeout(function () {
      if (!zdPage.isMobile) $('.txtSearch.active').focus();
      else $('.txtSearch.active').blur();
    }, 100);
  }

  // Invoked when search text changes; starts prefix query, handles result.
  function prefixTrigger() {
    // If this comes when IME is composing, bollocks. Don't interfere.
    if (isComposing) return;
    // Show, or maybe hide
    var query = $('.txtSearch.active').val();
    if (query.length < 3) {
      killPrefixHints();
      return;
    }
    // Query for hints
    var sentId = ++prefixReqId;
    var req = $.ajax({
      url: "/api/smarts/prefixhints",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { prefix: query }
    });
    req.done(function (data) {
      if (sentId != prefixReqId) return;
      // No suggestions: kill box
      if (data == null || data.length == 0) {
        killPrefixHints();
        return;
      }
      // Suggestions box
      $("body").append('<div id="searchSuggestions"></div>');
      $("#searchSuggestions").addClass("visible");
      $("#searchSuggestions").addClass("pending"); // If already visible, greys out old items while query is in progres
      // Positioning magic: in full version only
      if (!zdPage.isMobile()) {
        var sbOfs = $(".searchBox").offset();
        var sbWidth = $(".searchBox").width();
        var ssOfs = $("#searchSuggestions").offset();
        $("#searchSuggestions").offset({ left: sbOfs.left, top: ssOfs.top });
      }
      // Modal window management
      zdPage.modalShown(killPrefixHints);
      // Content (each suggestion)
      var toShow = "";
      for (var i = 0; i != data.length; ++i) {
        toShow += "<div class='prefix-suggestion'>" + escapeHTML(data[i].suggestion) + "</div>";
      }
      $("#searchSuggestions").html(toShow);
      $("#searchSuggestions").removeClass("pending");
      // Repopulation clears active item; keyboard navigation must start from zero position
      prefixActiveIx = -1;
      // Click event for insertion
      $(".prefix-suggestion").click(onPrefixClick);
    });
    req.fail(function (jqXHR, textStatus, error) {
      if (sentId != prefixReqId) return;
      killPrefixHints();
    });
  }

  // Handles keyboard up/down navigation of prefix suggestions list
  function onPrefixKeyUpDown(e, up) {
    // If this comes when IME is composing, bollocks. Don't interfere.
    if (isComposing) return;
    // Nothing to do if suggestions list is not even on screen
    if (!$("#searchSuggestions").hasClass("visible")) return;
    // This will get us Tab too
    var keycode = e.keycode || e.which;
    // Up/down: navigation
    if (!up && (keycode == 40 || keycode == 38)) {
      if (!$("#searchSuggestions").hasClass("visible")) return;
      var count = $("#searchSuggestions").children().length;
      if (keycode == 40)++prefixActiveIx;
      else --prefixActiveIx;
      if (prefixActiveIx < -1) prefixActiveIx = count - 1;
      else if (prefixActiveIx == count) prefixActiveIx = -1;
      // Highlight active item
      $(".prefix-suggestion").removeClass("active");
      if (prefixActiveIx >= 0) $("#searchSuggestions").children().eq(prefixActiveIx).addClass("active");
    }
    // Tab, Enter, Space all insert active item in different ways
    else if ((!up && (keycode == 9 || keycode == 32)) || (up && keycode == 13)) {
      // Only if we're navigating!
      if (prefixActiveIx < 0) return;
      prefixSuppressTrigger = true;
      var newVal = $(".prefix-suggestion.active").text();
      if (keycode == 32) newVal += " ";
      $(".txtSearch.active").val(newVal);
      $(".txtSearch.active").focus();
      prefixSuppressTrigger = false;
      killPrefixHints();
      e.preventDefault();
    }
    // Esc closes the list
    else if (!up && keycode == 27) killPrefixHints();
  }

  // An item in the search suggestions list is clicked.
  function onPrefixClick(evt) {
    // *NOT* doing the below. On click, page will call us to hide list: which is precisely what we want.
    //evt.stopPropagation();
    prefixSuppressTrigger = true;
    $(".txtSearch.active").val($(this).text());
    $(".txtSearch.active").focus();
    prefixSuppressTrigger = false;
  }

  // Hides prefix search hint element, resets any interactive state to nothing.
  function killPrefixHints() {
    zdPage.modalHidden();
    $("#searchSuggestions").remove();
    prefixActiveIx = -1;
  }

  // Show the search settings popup (generate from template; event wireup; position).
  function showSettings(event) {
    // Render HTML from template
    var html = zdSnippets["lookup.options"];
    html = html.replace("{{options-title}}", uiStrings["options-title"]);
    html = html.replace("{{options-script}}", uiStrings["options-script"]);
    html = html.replace("{{options-simplified}}", uiStrings["options-simplified"]);
    html = html.replace("{{options-traditional}}", uiStrings["options-traditional"]);
    html = html.replace("{{options-bothscripts}}", uiStrings["options-bothscripts"]);
    html = html.replace("{{options-tonecolors}}", uiStrings["options-tonecolors"]);
    html = html.replace("{{options-nocolors}}", uiStrings["options-nocolors"]);
    html = html.replace("{{options-pleco}}", uiStrings["options-pleco"]);
    html = html.replace("{{options-dummitt}}", uiStrings["options-dummitt"]);
    $("#searchOptionsBox").html(html);
    // Housekeeping; show search box
    zdPage.modalShown(hideSettings);
    var elmPopup = $("#searchOptionsBox");
    elmPopup.addClass("visible");
    $("#optionsClose").click(hideSettings);
    elmPopup.click(function (evt) { evt.stopPropagation(); });
    // Disable tooltip while settings are on screen
    $(".btnSettings").tooltipster('disable');
    // Stop event propagation, or we'll be closed right away
    event.stopPropagation();
    // Full version: position search box to settings button
    // Mobile: centered, fixed
    if (!zdPage.isMobile()) {
      var elmStgs = $(".btnSettings");
      var rectStgs = [elmStgs.offset().left, elmStgs.offset().top, elmStgs.width(), elmStgs.height()];
      var elmTail = $("#optionsTail");
      var rectTail = [elmTail.offset().left, elmTail.offset().top, elmTail.width(), elmTail.height()];
      var xMidStgs = rectStgs[0] + rectStgs[2] / 2.2;
      var xMidTail = rectTail[0] + rectTail[2] / 2;
      elmPopup.offset({ left: elmPopup.offset().left + xMidStgs - xMidTail, top: elmPopup.offset().top });
    }
    // Load persisted/default values; update UI
    loadOptions();
    // Events
    optionsEventWireup();
  }

  // Hides the search settings popup
  function hideSettings() {
    $("#searchOptionsBox").removeClass("visible");
    zdPage.modalHidden();
    // Re-enable tooltip
    if (!zdPage.isMobile()) $(".btnSettings").tooltipster('enable');
  }

  // Load options (or inits to defaults); updates UI.
  function loadOptions() {
    // Check cookie for script
    var ckScript = localStorage.getItem("uiscript");
    if (ckScript !== null) optScript = ckScript;
    if (optScript === "simp") $("#optScriptSimplified").addClass("selected");
    else if (optScript === "trad") $("#optScriptTraditional").addClass("selected");
    else if (optScript === "both") $("#optScriptBoth").addClass("selected");
    // Check cookie for tone colors
    var ckTones = localStorage.getItem("uitones");
    if (ckTones !== null) optTones = ckTones;
    if (optTones === "none") $("#optToneColorsNone").addClass("selected");
    else if (optTones === "pleco") $("#optToneColorsPleco").addClass("selected");
    else if (optTones === "dummitt") $("#optToneColorsDummitt").addClass("selected");
  }

  // Interactions of options UI.
  function optionsEventWireup() {
    // Event handlers for mouse (desktop)
    var handlersMouse = {
      mousedown: function (e) {
        $(this).animate({ backgroundColor: clrEmph }, 200);
      },
      click: function (e) {
        $(this).animate({ backgroundColor: clrSel }, 400);
        selectOption(this.id);
      },
      mouseenter: function (e) {
        $(this).animate({ backgroundColor: clrSel }, 400);
      },
      mouseleave: function (e) {
        var clr = $(this).hasClass('selected') ? clrSel : "transparent";
        $(this).animate({ backgroundColor: clr }, 400);
      }
    };
    // Event handlers for mobile (touch)
    var handlersTouch = {
      click: function (e) {
        $(this).animate({ backgroundColor: clrSel }, 400);
        selectOption(this.id);
      }
    };
    var handlers = zdPage.isMobile() ? handlersTouch : handlersMouse;
    // Script option set
    $("#optScriptSimplified").on(handlers);
    $("#optScriptTraditional").on(handlers);
    $("#optScriptBoth").on(handlers);

    // Tone colors option set
    $("#optToneColorsNone").on(handlers);
    $("#optToneColorsPleco").on(handlers);
    $("#optToneColorsDummitt").on(handlers);
  }

  // Handler: an option is selected (clicked) in the options UI.
  function selectOption(id) {
    function unselectOption(optId) {
      if ($(optId).hasClass("selected")) {
        $(optId).removeClass("selected");
        $(optId).animate({ backgroundColor: "transparent" }, 400);
      }
    }
    // Script option set
    if (id.indexOf("optScript") === 0) {
      unselectOption("#optScriptSimplified");
      unselectOption("#optScriptTraditional");
      unselectOption("#optScriptBoth");
    }
      // Tone colors option set
    else if (id.indexOf("optToneColors") === 0) {
      unselectOption("#optToneColorsNone");
      unselectOption("#optToneColorsPleco");
      unselectOption("#optToneColorsDummitt");
    }
    $("#" + id).addClass("selected");
    // Store: Script options
    if (id === "optScriptSimplified") localStorage.setItem("uiscript", "simp");
    else if (id === "optScriptTraditional") localStorage.setItem("uiscript", "trad");
    else if (id === "optScriptBoth") localStorage.setItem("uiscript", "both");
    // Store: Tone color options
    if (id === "optToneColorsNone") localStorage.setItem("uitones", "none");
    else if (id === "optToneColorsPleco") localStorage.setItem("uitones", "pleco");
    else if (id === "optToneColorsDummitt") localStorage.setItem("uitones", "dummitt");
    // Load options again: adds "selected" to element (redundantly), and updates UI.
    loadOptions();
  }

  // Shows the handwriting recognition pop-up.
  function showStrokeInput(event) {
    var hwEnabled = false;
    // Firsty first: load the stroke data if missing
    if (typeof zdCharData === 'undefined') {
      // Add element, and also event handler for completion
      var elmStrokes = document.createElement('script');
      document.getElementsByTagName("head")[0].appendChild(elmStrokes);
      var funEnabler = function () {
        zdHandwriting.setEnabled(true);
        sessionStorage.setItem("charDataLoaded", true);
      }
      elmStrokes.onload = function () { funEnabler(); };
      elmStrokes.onreadystatechange = function () {
        if (this.readyState == 'complete') funEnabler();
      }
      elmStrokes.setAttribute("type", "text/javascript");
      elmStrokes.setAttribute("src", "/prod-js/xcharacterdata.js");
    }
    // If stroke data is alreayd loaded, explicitly call setEnabled - this makes sure character matcher is initialized
    else hwEnabled = true;

    // Render HTML from template
    var html = zdSnippets["lookup.handwriting"];
    // TO-DO: Loca
    $("#handwritingBox").html(html);
    // Housekeeping; show search box
    zdPage.modalShown(hideStrokeInput);
    var elmPopup = $("#handwritingBox");
    elmPopup.addClass("visible");
    $("#hwClose").click(hideStrokeInput);
    elmPopup.click(function (evt) { evt.stopPropagation(); });
    // Disable tooltip while settings are on screen
    $(".btnWrite").tooltipster('disable');
    // Stop event propagation, or we'll be closed right away
    event.stopPropagation();
    // Full version: Position search box to settings button
    if (!zdPage.isMobile()) {
      var elmStgs = $(".btnWrite");
      var rectStgs = [elmStgs.offset().left, elmStgs.offset().top, elmStgs.width(), elmStgs.height()];
      var elmTail = $("#hwTail");
      var rectTail = [elmTail.offset().left, elmTail.offset().top, elmTail.width(), elmTail.height()];
      var xMidStgs = rectStgs[0] + rectStgs[2] / 2.2;
      var xMidTail = rectTail[0] + rectTail[2] / 2;
      elmPopup.offset({ left: elmPopup.offset().left + xMidStgs - xMidTail, top: elmPopup.offset().top });
    }
    var strokeCanvasWidth = $("#stroke-input-canvas").width();
    $("#stroke-input-canvas").css("height", strokeCanvasWidth);
    var canvasElement = document.getElementById("stroke-input-canvas");
    canvasElement.width = strokeCanvasWidth;
    canvasElement.height = strokeCanvasWidth;
    $("#suggestions").css("height", $("#suggestions").height());

    // Initialize handwriting logic. Element IDs provided as params object.
    zdHandwriting.init({
      canvasId: "stroke-input-canvas",
      suggestionsId: "suggestions",
      suggestionClass: "sugItem",
      insertionTargedClass: "txtSearch"
    });
    zdHandwriting.setEnabled(hwEnabled);
    zdHandwriting.clearCanvas();
    $("#strokeClear").click(zdHandwriting.clearCanvas);
    $("#strokeUndo").click(zdHandwriting.undoStroke);
  }

  // Hides the handwriting recognition popup
  function hideStrokeInput() {
    $("#handwritingBox").empty();
    $("#handwritingBox").removeClass("visible");
    zdPage.modalHidden();
  }

  // Clears the search field
  function clearSearch() {
    $(".txtSearch").val("");
    $(".txtSearch.active").focus();
  }

  // When the search input field receives focus
  function txtSearchFocus(event) {
    if (!$(this).hasClass("active")) return;
    if (prefixSuppressTrigger) return;
    $(".txtSearch.active").select();
  }

  // Returns object with search params.
  function getSearchParams() {
    return { searchScript: optScript, searchTones: optTones };
  }

  // Submits a dictionary search as simple GET URL
  function submitSearch() {
    var queryStr = $('.txtSearch.active').val();
    queryStr = queryStr.replace(" ", "+");
    killPrefixHints();
    hideStrokeInput();
    zdPage.submitSearch(queryStr);
  }

  // Dynamically position stroke order animation popup in Desktop
  function dynPosSOA(zis) {
    // First, decide if we're showing box to left or right of character
    var hanziOfs = zis.offset();
    var onRight = hanziOfs.left < $(document).width() / 2;
    var left = hanziOfs.left + zis.width() + 20;
    if (onRight) $("#soaBox").removeClass("soaBoxLeft");
    else {
      $("#soaBox").addClass("soaBoxLeft");
      left = hanziOfs.left - $("#soaBox").width() - 20;
    }
    // Decide about Y position. Box wants char to be at vertical middle
    // But is willing to move up/down to fit in content area
    var charY = hanziOfs.top + zis.height() / 2;
    var boxH = $("#soaBox").height();
    var top = charY - boxH / 2;
    // First, nudge up if we stretch beyond viewport bottom
    var wBottom = window.pageYOffset + window.innerHeight - 10;
    if (top + boxH > wBottom) top = wBottom - boxH;
    // Then, nudge down if we're over the ceiling
    //var wTop = $(".hdrSearch").position().top + $("#hdrSearch").height() + window.pageYOffset + 20;
    var wTop = $("#headermask").position().top + $("#headermask").height() + window.pageYOffset;
    if (top < wTop) top = wTop;
    // Position box, and tail
    $("#soaBox").offset({ left: left, top: top });
    $("#soaBoxTail").css("top", (charY - top - 10) + "px");
  }

  // Positions and shows the stroke animation pop-up for the clicked hanzi.
  function showStrokeAnim(event) {
    // We get click event when mouse button is released after selecting a single hanzi
    // Don't want to show pop-up in this edge case
    var sbe = zdPage.getSelBoundElm();
    if (sbe != null && sbe.textContent == $(this).text())
      return;
    // OK, so we're actually showing. Stop propagation so we don't get auto-hidden.
    event.stopPropagation();
    // If previous show's in progress, kill it
    // Also kill stroke input, in case it's shown
    zdStrokeAnim.kill();
    // Start the whole spiel
    $("#soaBox").css("display", "block");
    // We only position dynamically in desktop version; in mobile, it's fixed
    if (!zdPage.isMobile()) dynPosSOA($(this));
    // Render grid, issue AJAX query for animation data
    zdStrokeAnim.renderBG();
    zdStrokeAnim.startQuery($(this).text());
    // Notify page
    zdPage.modalShown(hideStrokeAnim);
  }

  // Closes the stroke animation pop-up (if shown).
  function hideStrokeAnim() {
    zdStrokeAnim.kill();
    $("#soaBox").css("display", "none");
    zdPage.modalHidden();
  }
})();


