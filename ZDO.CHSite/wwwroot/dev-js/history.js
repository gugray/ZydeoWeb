/// <reference path="x-jquery-2.1.4.min.js" />
/// <reference path="x-jquery.color-2.1.2.min.js" />
/// <reference path="x-jquery.tooltipster.min.js" />
/// <reference path="strings-hu.js" />
/// <reference path="page.js" />

var zdHistory = (function () {
  "use strict";

  $(document).ready(function () {
    zdPage.registerInitScript("edit/history", init);
  });

  function init() {
    // Add tooltips to pliant per-entry commands
    $(".opHistComment").tooltipster({
      content: $("<span>" + uiStrings["history"]["tooltip-comment"] + "</span>"),
      position: 'left'
    });
    $(".opHistEdit").tooltipster({
      content: $("<span>" + uiStrings["history"]["tooltip-edit"] + "</span>"),
      position: 'left'
    });
    $(".opHistFlag").tooltipster({
      content: $("<span>" + uiStrings["history"]["tooltip-flag"] + "</span>"),
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
    bodyHtml = bodyHtml.replace("{{hint}}", uiStrings["history-commententry-hint"]);
    var params = {
      id: "dlgHistComment",
      title: uiStrings["history.addComment"]["title"],
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
    var req = $.ajax({
      url: "/Handler.ashx",
      type: "POST",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { action: "history_commententry", entry_id: entryId }
    });
    req.done(function (data) {
      zdPage.showAlert(uiStrings["history.addComment"]["successtitle"], uiStrings["history.addComment"]["successmessage"], false);
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.showAlert(uiStrings["history.addComment"]["failtitle"], uiStrings["history.addComment"]["failmessage"], true);
    });
    return true;
  }

})();
