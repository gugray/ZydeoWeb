/// <reference path="../lib/jquery-3.1.1.min.js" />
/// <reference path="zsnippets.js" />
/// <reference path="page.js" />

var App = App || {};

App.xlate = (function (path) {
  "use strict";

  var path = path;
  enter();

  function enter() {
    $("#txtSearch").select();
    setTimeout(function () {
      $("#txtSearch").focus();
    }, 50);
    $("#txtSearch").keypress(function (e) {
      if ((e.keyCode || e.which) == 13) {
        onSubmit();
        return false;
      }
    });
  }

  function onSubmit() {
    var request = $.ajax({
      url: "/api/search/go",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { query: $("#txtSearch").val() }
    });
    request.done(function (data) {
      render(data);
    });
    request.fail(function (xhr, status, error) {
      $(".results").text("Bunny is sad.");
    });
  }

  function render(data) {
    var html = "";
    for (var i = 0; i != data.length; ++i) {
      var hit = data[i];
      html += "<div class='item'>";
      html += "<div class='source'>";
      var spanClosed = true;
      for (var j = 0; j != hit.source.length; ++j) {
        if (j == hit.srcHiStart) {
          html += "<span>";
          spanClosed = false;
        }
        if (j == hit.srcHiStart + hit.srcHiLen) {
          html += "</span>";
          spanClosed = true;
        }
        html += App.page.esc(hit.source[j]);
      }
      if (!spanClosed) {
        html += "</span>";
        spanClosed = true;
      }
      html += "</div>";
      html += "<div class='target'>";
      spanClosed = true;
      for (var j = 0; j != hit.target.length; ++j) {
        if (hiliteEnds(hit, j)) {
          html += "</span>";
          spanClosed = true;
        }
        var score = hiliteStarts(hit, j);
        if (score >= 10) {
          var cls = "hlLo";
          if (score > 100) cls = "hlMid";
          if (score > 300) cls = "hlHi";
          html += "<span data-score='" + score + "' class='" + cls + "'>";
          spanClosed = false;
        }
        html += App.page.esc(hit.target[j]);
      }
      if (!spanClosed) {
        html += "</span>";
        spanClosed = true;
      }
      html += "</div>";
      html += "</div>";
    }
    $(".results").html(html);
    $(".item .source span").mouseenter(function () {
      var pairs = ($(this).data("pairixs") + "").split(" ");
      var elmTarget = $(this).closest(".item").find(".target");
      for (var i = 0; i != pairs.length; ++i) {
        var elmPair = elmTarget.find("[data-ix='" + pairs[i] + "']");
        elmPair.addClass("hilite");
      }
    });
    $(".item .source span").mouseleave(function () {
      $(".item .target span").removeClass("hilite");
    });
  }

  function hiliteStarts(hit, pos) {
    for (var i = 0; i != hit.trgHilights.length; ++i) {
      if (hit.trgHilights[i].start == pos) return hit.trgHilights[i].score;
    }
    return -1;
  }

  function hiliteEnds(hit, pos) {
    for (var i = 0; i != hit.trgHilights.length; ++i) {
      if (hit.trgHilights[i].start + hit.trgHilights[i].len == pos) return true;
    }
    return false;
  }

  function getPairs(ix, map) {
    var pairs = "";
    for (var i = 0; i != map.length; ++i) {
      if (map[i][0] != ix) continue;
      if (pairs.length > 0) pairs += " ";
      pairs += "" + map[i][1];
    }
    return pairs;
  }

  return {
    enter: enter,
    name: "main"
  };
});

App.page.registerPage({
  name: "main",
  isMyRoute: function (path) {
    if (path == "" || path == "/") return true;
    return false;
  },
  getController: function (path) {
    return App.xlate(path);
  }
});
