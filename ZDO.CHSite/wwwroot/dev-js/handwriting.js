/// <reference path="x-jquery-2.1.4.min.js" />
/// <reference path="charmatcher.js" />

var zdHandwriting = (function () {
  "use strict";

  var prms = null;

  // Global options ******************************
  // Width of strokes drawn on screen
  var strokeWidth = 5;

  var canvas;
  var ctx;
  var clicking = false;
  var lastTouchX = -1;
  var lastTouchY = -1;
  var tstamp;
  var lastPt;
  var recogEnabled = true;
  var appendNotOverwrite = false;

  // An array of arrays; each element is the coordinate sequence for one stroke from the canvas
  // Where "stroke" is everything between button press - move - button release
  var rawStrokes = [];

  // Canvas coordinates of each point in current stroke, in raw (unanalyzed) form.
  var currentStroke = null;

  // Interaction log for current session
  var uxLog = {
    timeShown: null, // Timestamp when UI was shown (msec counter)
    pickedInSession: false, // True if at least 1 char has been picked since session started
    drawingActions: [] // Drawing actions: { action: "stroke", points: [], results: [] } or { action: "undo" } or { action: "clear" }
  };

  // Draws a clear canvas, with gridlines
  function drawClearCanvas() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.setLineDash([1, 1]);
    ctx.lineWidth = 0.5;
    ctx.strokeStyle = "grey";
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(canvas.width, 0);
    ctx.lineTo(canvas.width, canvas.height);
    ctx.lineTo(0, canvas.height);
    ctx.lineTo(0, 0);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(canvas.width, canvas.height);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(canvas.width, 0);
    ctx.lineTo(0, canvas.height);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(canvas.width / 2, 0);
    ctx.lineTo(canvas.width / 2, canvas.height);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(0, canvas.height / 2);
    ctx.lineTo(canvas.width, canvas.height / 2);
    ctx.stroke();
  }

  function startClick(x, y) {
    clicking = true;
    currentStroke = [];
    lastPt = [x, y];
    currentStroke.push(lastPt);
    ctx.strokeStyle = "black";
    ctx.setLineDash([]);
    ctx.lineWidth = strokeWidth;
    ctx.beginPath();
    ctx.moveTo(x, y);
    tstamp = new Date();
  }

  function dragClick(x, y) {
    if ((new Date().getTime() - tstamp) < 50) return;
    tstamp = new Date();
    var pt = [x, y];
    if ((pt[0] == lastPt[0]) && (pt[1] == lastPt[1])) return;
    currentStroke.push(pt);
    lastPt = pt;
    ctx.lineTo(x, y);
    ctx.stroke();
  }

  function endClick(x, y) {
    clicking = false;
    if (x == -1) return;
    ctx.lineTo(x, y);
    ctx.stroke();
    currentStroke.push([x, y]);
    // Store stroke
    rawStrokes.push(currentStroke);
    currentStroke = [];
    // Character lookup
    var matches = matchAndShow();
    // Log interaction
    var loggedAction = {
      action: "stroke",
      points: [], //rawStrokes[rawStrokes.length - 1],
      results: matches
    }
    for (var i = 0; i != rawStrokes[rawStrokes.length - 1].length; ++i) {
      var pt = rawStrokes[rawStrokes.length - 1][i];
      var ptRound = [Math.round(pt[0]), Math.round(pt[1])];
      loggedAction.points.push(ptRound);
    }
    uxLog.drawingActions.push(loggedAction);
  }

  // Redraws raw strokes on the canvas.
  function redrawInput() {
    for (var i1 in rawStrokes) {
      ctx.strokeStyle = "black";
      ctx.setLineDash([]);
      ctx.lineWidth = strokeWidth;
      ctx.beginPath();
      ctx.moveTo(rawStrokes[i1][0][0], rawStrokes[i1][0][1]);
      var len = rawStrokes[i1].length;
      for (var i2 = 0; i2 < len - 1; i2++) {
        ctx.lineTo(rawStrokes[i1][i2][0], rawStrokes[i1][i2][1]);
        ctx.stroke();
      }
      ctx.lineTo(rawStrokes[i1][len - 1][0], rawStrokes[i1][len - 1][1]);
      ctx.stroke();
    }
  }

  function uxLogSubmit(selectedChar, selectedIx) {
    // Don't submit "closed with no actions" event after sucessful session
    if (uxLog.pickedInSession && uxLog.drawingActions.length == 0) {
      // NOP
    }
    else {
      // The actual interaction log that we submit
      var obj = {
        char: selectedChar,
        ix: selectedIx,
        duration: (new Date().getTime() - uxLog.timeShown),
        actions: uxLog.drawingActions
      };
      var json = JSON.stringify(obj);
      // Submit it
      var req = $.ajax({
        url: "/api/smarts/handwritingfinished",
        type: "POST",
        contentType: "application/x-www-form-urlencoded; charset=UTF-8",
        data: { json: json }
      });
    }
    // Clear UX log
    uxLog.timeShown = new Date().getTime();
    uxLog.drawingActions = [];
  }

  function matchAndShow() {
    //var matches = zdCharMatcher.match(rawStrokes, 8);
    var analyzedChar = new HanziLookup.AnalyzedCharacter(rawStrokes);
    var matcherMMAH = new HanziLookup.Matcher("mmah");
    matcherMMAH.match(analyzedChar, 8, function (scoredMatches) {
      var matches = [];
      for (var i = 0; i != scoredMatches.length; ++i) matches.push(scoredMatches[i].character);
      $("#" + prms.suggestionsId).html('');
      for (var i = 0; ((i < 8) && matches[i]) ; i++) {
        var sug = document.createElement('span');
        $(sug).append(matches[i]).attr('class', prms.suggestionClass).data("matchix", i);
        $(sug).click(function () {
          // Complete and submit interaction log
          uxLog.pickedInSession = true;
          uxLogSubmit($(this).text(), $(this).data("matchix"));
          // Insert user's pick into search field
          if (appendNotOverwrite)
            $("." + prms.insertionTargedClass).val($("." + prms.insertionTargedClass).val() + $(this).html());
          else
            $("." + prms.insertionTargedClass).val($(this).html());
          appendNotOverwrite = true;
          clearCanvas();
          $("#" + prms.suggestionsId).html('');
        });
        $("#" + prms.suggestionsId).append(sug);
      }
      return matches;
    });
  }

  function clearCanvas() {
    // Redraw canvas (gridlines)
    drawClearCanvas();
    // Clear previous suggestions
    $("#" + prms.suggestionsId).html('');
    // Reset gathered strokes input
    rawStrokes = [];
  }

  return {
    // Initializes handwriting recognition (events etc.)
    init: function (params) {
      uxLog.timeShown = new Date().getTime();
      uxLog.timeClosed = null;
      uxLog.drawingActions = [];
      uxLog.pickedInSession = false;

      prms = params;
      canvas = document.getElementById(prms.canvasId);
      if (canvas === null) return;
      ctx = canvas.getContext("2d");

      $('#' + prms.canvasId).mousemove(function (e) {
        if (!clicking || !recogEnabled) return;
        var x = e.pageX - $(this).offset().left;
        var y = e.pageY - $(this).offset().top;
        dragClick(x, y);
      });
      $('#' + prms.canvasId).mousedown(function (e) {
        if (!recogEnabled) return;
        var x = e.pageX - $(this).offset().left;
        var y = e.pageY - $(this).offset().top;
        startClick(x, y);
      }).mouseup(function (e) {
        if (!recogEnabled) return;
        var x = e.pageX - $(this).offset().left;
        var y = e.pageY - $(this).offset().top;
        endClick(x, y);
      });

      $('#' + prms.canvasId).bind("touchmove", function (e) {
        if (!clicking || !recogEnabled) return;
        e.preventDefault();
        var x = e.originalEvent.touches[0].pageX - $(this).offset().left;
        lastTouchX = x;
        var y = e.originalEvent.touches[0].pageY - $(this).offset().top;
        lastTouchY = y;
        dragClick(x, y);
      });
      $('#' + prms.canvasId).bind("touchstart", function (e) {
        if (!recogEnabled) return;
        e.preventDefault();
        document.activeElement.blur();
        var x = e.originalEvent.touches[0].pageX - $(this).offset().left;
        var y = e.originalEvent.touches[0].pageY - $(this).offset().top;
        startClick(x, y);
      }).bind("touchend", function (e) {
        if (!recogEnabled) return;
        e.preventDefault();
        document.activeElement.blur();
        endClick(lastTouchX, lastTouchY);
        lastTouchX = lastTouchY = -1;
      });
    },

    // Clear canvas and resets gathered strokes data for new input.
    clearCanvas: function () {
      // Start afresh
      clearCanvas();
      // Log interaction
      if (uxLog.drawingActions.length > 0) uxLog.drawingActions.push({ action: "clear" });
    },

    // Called just before popup is hidden, so we can submit and reset UX log
    endSession: function () {
      uxLogSubmit("X", -1);
    },

    // Undoes the last stroke input by the user.
    undoStroke: function () {
      // Sanity check: nothing to do if input is empty (no strokes yet)
      if (rawStrokes.length == 0) return;
      // Remove last stroke
      rawStrokes.length = rawStrokes.length - 1;
      // Clear canvas
      drawClearCanvas();
      // Redraw input (raw strokes) from scratch
      redrawInput();
      // Lookup best matching characters for what's left on canvas now
      matchAndShow();
      // Log interaction
      uxLog.drawingActions.push({ action: "clear" });
    },

    // Sets whether inserting match should overwrite search field, or append to it.
    setInsertionMode: function (append) { appendNotOverwrite = append; },

    // Sets enabled/disabled state of handwriting recognition. (Disabled until character data is dynamically loaded.)
    setEnabled: function(enabled) {
      recogEnabled = enabled;
      if (!enabled) {
        $("#stroke-input-canvas").addClass("loading");
        $("#strokeDataLoading").css("display", "block");
      }
      else {
        $("#stroke-input-canvas").removeClass("loading");
        $("#strokeDataLoading").css("display", "none");
        //zdCharMatcher.init(zdCharData)
      }
    }

  };

})();
