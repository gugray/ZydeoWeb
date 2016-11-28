/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/jquery.color-2.1.2.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="strings.en.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdHistory = (function () {
  "use strict";

  zdPage.registerInitScript("edit/history", init);

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
    $(".revealPast").tooltipster({
      content: $("<span>" + zdPage.ui("history", "tooltip-revealpast") + "</span>"),
      position: 'top'
    });
    // Event handlers for per-entry commands
    $(".opHistComment").click(onComment);
    $(".revealPast").click(onRevealPast);
    // Touch: hover simulation
    if (zdPage.isTouch()) {
      $(".historyItem").bind("touchstart", function (e) {
        $(".historyItem").removeClass("tapOver");
        $(this).addClass("tapOver");
      });
    }
  }

  function onRevealPast(evt) {
    // Find entry ID in parent with historyItem class
    var sender = $(this);
    var elm = sender;
    while (!elm.hasClass("historyItem")) elm = elm.parent();
    var entryId = elm.data("entry-id");
    // Get history
    var req = zdAuth.ajax("/api/edit/getpastchanges", "GET", { entryId: entryId, lang: zdPage.getLang() });
    req.done(function (data) {
      if (!data) {
        zdPage.showAlert(
          zdPage.ui("history", "retrievePastFailCaption"),
          zdPage.ui("history", "retrievePastFailMsg"),
          true);
      }
      else {
        sender.off("click", onRevealPast);
        sender.tooltipster("disable");
        $(data).insertAfter(elm);
      }
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.showAlert(
        zdPage.ui("history", "retrievePastFailCaption"),
        zdPage.ui("history", "retrievePastFailMsg"),
        true);
    });
  }

  function onComment(evt) {
    // Pester to log in
    if (!zdAuth.isLoggedIn()) {
      zdAuth.showLogin(zdPage.ui("history", "loginToComment"));
      evt.stopPropagation();
      return;
    }

    // Find entry ID in parent with historyItem class
    var elm = $(this);
    while (!elm.hasClass("historyItem")) elm = elm.parent();
    var entryId = elm.data("entry-id");
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
    var req = zdAuth.ajax("/api/edit/commententry", "POST", { entryId: entryId, note: cmt });
    zdPage.setModalWorking("#dlgHistComment", true);
    req.done(function (data) {
      zdPage.closeModal("txtHistComment");
      if (data && data === true)
        zdPage.showAlert(zdPage.ui("history.addComment", "successtitle"), uiStrings["history.addComment"]["successmessage"], false);
      else
        zdPage.showAlert(zdPage.ui("history.addComment", "failtitle"), uiStrings["history.addComment"]["failmessage"], true);
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.closeModal("txtHistComment");
      zdPage.showAlert(zdPage.ui("history.addComment", "failtitle"), uiStrings["history.addComment"]["failmessage"], true);
    });
    return true;
  }

})();
