﻿/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/jquery.color-2.1.2.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="strings.en.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdHistory = (function () {
  "use strict";

  $(document).ready(function () {
    zdPage.registerInitScript("edit/history", init);
  });

  function init() {
    // Add tooltips to pliant per-entry commands
    $(".opHistComment").tooltipster({
      content: $("<span>" + zdPage.ui("history", "tooltip-comment") + "</span>"),
      position: 'left'
    });
    $(".opHistEdit").tooltipster({
      content: $("<span>" + zdPage.ui("history", "tooltip-edit") + "</span>"),
      position: 'left'
    });
    $(".opHistFlag").tooltipster({
      content: $("<span>" + zdPage.ui("history", "tooltip-flag") + "</span>"),
      position: 'left'
    });
    // Event handlers for per-entry commands
    $(".opHistComment").click(onComment);
  }

  function onComment(evt) {
    // Find entry ID in parent with historyItem class
    var elm = $(this);
    while (!elm.hasClass("historyItem")) elm = elm.parent();
    var entryId = elm.data("entryid");
    // Prepare modal window content
    var bodyHtml = zdSnippets["history.addComment"];
    bodyHtml = bodyHtml.replace("{{hint}}", zdPage.ui("history.addComment", "hint"));
    var params = {
      id: "dlgHistComment",
      title: zdPage.ui("history.addComment", "title"),
      body: bodyHtml,
      confirmed: function () { return onCommentConfirmed(entryId); },
      toFocus: "#txtHistComment"
    };
    // Show
    zdPage.showModal(params);
    evt.stopPropagation();
  }

  function onCommentConfirmed(entryId) {
    var cmt = $("#txtHistComment").val();
    if (cmt.length == 0) {
      return false;
    }
    var req = zdAuth.ajax("!!history_commententry", "POST", { action: "history_commententry", entry_id: entryId });
    req.done(function (data) {
      zdPage.showAlert(zdPage.ui("history.addComment", "successtitle"), uiStrings["history.addComment"]["successmessage"], false);
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.showAlert(zdPage.ui("history.addComment", "failtitle"), uiStrings["history.addComment"]["failmessage"], true);
    });
    return true;
  }

})();
