/// <reference path="../lib/jquery-3.1.1.min.js" />
/// <reference path="zsnippets.js" />
/// <reference path="page.js" />

var App = App || {};

App.xlate = (function (path) {
  "use strict";

  var path = path;
  enter();

  function enter() {
    var request = $.ajax({
      url: "/api/search/go",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { query: "sailor" }
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
      for (var j = 0; j != hit.srcTokens.length; ++j) {
        var pairs = getPairs(j, hit.map);
        var tok = hit.srcTokens[j];
        if (j != 0) html += " ";
        if (pairs != "") html += "<span data-pairixs='" + pairs + "'>";
        html += App.page.esc(tok);
        if (pairs != "") html += "</span>";
      }
      html += "</div>";
      html += "<div class='target'>";
      for (var j = 0; j != hit.trgTokens.length; ++j) {
        if (j != 0) html += " ";
        html += "<span data-ix='" + j + "'>";
        var tok = hit.trgTokens[j];
        html += App.page.esc(tok);
        html += "</span>";
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
