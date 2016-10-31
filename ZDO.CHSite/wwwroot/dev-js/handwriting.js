/// <reference path="x-jquery-2.1.4.min.js" />
/// <reference path="charmatcher.js" />

var zdHandwriting = (function () {
  "use strict";

  var prms = null;

  // Global options ******************************
  // Width of strokes drawn on screen, for desktop browsers
  var strokeWidthDesktop = 5;
  // Width of strokes drawn on screen, for mobile browsers
  var strokeWidthMobile = 15;

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
    ctx.lineWidth = strokeWidthDesktop;
    //if (isMobile) ctx.lineWidth = strokeWidthMobile;
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
    rawStrokes.push(currentStroke);
    currentStroke = [];
    // Character lookup
    matchAndShow();
  }

  // Redraws raw strokes on the canvas.
  function redrawInput() {
    for (var i1 in rawStrokes) {
      ctx.strokeStyle = "black";
      ctx.setLineDash([]);
      ctx.lineWidth = strokeWidthDesktop;
      //if (isMobile) ctx.lineWidth = strokeWidthMobile;
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

  function matchAndShow() {
    var matches = zdCharMatcher.match(rawStrokes, 8);
    $("#" + prms.suggestionsId).html('');
    for (var i = 0; ((i < 8) && matches[i]) ; i++) {
      var sug = document.createElement('span');
      $(sug).append(matches[i]).attr('class', prms.suggestionClass);
      $(sug).click(function () {
        if (appendNotOverwrite)
          $("#" + prms.insertionTargedId).val($("#" + prms.insertionTargedId).val() + $(this).html());
        else
          $("#" + prms.insertionTargedId).val($(this).html());
        appendNotOverwrite = true;
        clearCanvas();
        $("#" + prms.suggestionsId).html('');
      });
      $("#" + prms.suggestionsId).append(sug);
    }
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
    clearCanvas: function () { clearCanvas(); },

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
        zdCharMatcher.init(zdCharData)
      }
    }

  };

})();
