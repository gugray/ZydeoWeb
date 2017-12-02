/// <reference path="../lib/jquery-3.1.1.min.js" />

var App = App || {};

App.page = (function () {
  "use strict";

  var pages = [];
  var path = null; // (Current) path after domain name
  var ctrl = null; // Controller for current path

  $(document).ready(function () {
    // Where am I? Also instantiates controller.
    parseLocation();
    $(window).on('popstate', function (e) {
      parseLocation();
    });
  });

  function parseLocation() {
    var loc = window.history.location || window.location;
    var rePath = /https?:\/\/[^\/]+(.*)/i;
    var match = rePath.exec(loc);
    path = match[1];
    var found = false;
    for (var i = 0; i < pages.length; ++i) {
      if (pages[i].isMyRoute(path)) {
        if (ctrl != null && ctrl.name == pages[i].name) ctrl.move(path);
        else ctrl = pages[i].getController(path);
        found = true;
        break;
      }
    }
    if (!found) showBadpage();
    initDynNav();
  }

  function onDynNav() {
    $(".popup").removeClass("visible");
    history.pushState(null, null, this.href);
    parseLocation();
    return false;
  }

  function inPageNavigate(path) {
    $(".popup").removeClass("visible");
    history.pushState(null, null, path);
    parseLocation();
  }

  function initDynNav() {
    // Remove old handlers
    $(document).off('click', 'a.ajax', onDynNav);
    // Re-add handlers
    $(document).on('click', 'a.ajax', onDynNav);
  }

  function showBadpage() {
    $(".content-inner").html(zsnippets["badpage"]);
  }

  function esc(s) {
    return s.replace(/&/g, '&amp;')
      .replace(/"/g, '&quot;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
  }


  return {
    registerPage: function (ctrl) {
      pages.push(ctrl);
    },
    esc: esc,
    path: function () { return path; },
    showBadpage: showBadpage,
    inPageNavigate: inPageNavigate,
    reEnterCurrent: function () { ctrl.enter(); },
    startsWith: function (str, prefix) {
      if (str.length < prefix.length)
        return false;
      for (var i = prefix.length - 1; (i >= 0) && (str[i] === prefix[i]) ; --i)
        continue;
      return i < 0;
    }
  };

})();
