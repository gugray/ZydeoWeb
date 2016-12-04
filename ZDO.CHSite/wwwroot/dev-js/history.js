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
    // Repeat exactly this in reloadItem()!
    // --------------------------------------------------
    // Infuse context into displayed entries
    $(".entry").addClass("history");
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
    // In mobile, disable those tooltips
    if (zdPage.isMobile()) {
      $(".opHistComment").tooltipster("disable");
      $(".opHistEdit").tooltipster("disable");
      $(".opHistFlag").tooltipster("disable");
      $(".revealPast").tooltipster("disable");
    }
    // Event handlers for per-entry commands
    $(".opHistComment").click(onComment);
    //$(".opHistEdit").click(onEdit);
    $(".opHistFlag").click(onFlag);
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

  //function onEdit(evt) {
  //  // Find entry ID in parent with historyItem class
  //  var elm = $(this);
  //  while (!elm.hasClass("historyItem")) elm = elm.parent();
  //  var entryId = elm.data("entry-id");
  //  // Open editor
  //  zdPage.navigate("edit/existing/" + entryId);
  //  //window.open("/" + zdPage.getLang() + "/edit/existing/" + entryId);
  //}

  function onFlag(evt) {
    // Pester to log in
    if (!zdAuth.isLoggedIn()) {
      zdAuth.showLogin(zdPage.ui("history", "loginToEdit"));
      evt.stopPropagation();
      return;
    }
    // Find entry ID in parent with historyItem class
    var elm = $(this);
    while (!elm.hasClass("historyItem")) elm = elm.parent();
    var entryId = elm.data("entry-id");
    // Is there an element with class "entryStatus flagged" among children?
    var flagged = elm.find(".entryStatus.flagged").length > 0;

    // FLAG command
    if (!flagged) {
      // Prepare modal window content
      var bodyHtml = zdSnippets["history.flagEntry"];
      bodyHtml = bodyHtml.replace("{{hint}}", zdPage.ui("history.flagEntry", "hint"));
      var params = {
        id: "dlgHistFlag",
        title: zdPage.ui("history.flagEntry", "title"),
        body: bodyHtml,
        confirmed: function () { return onSubmitFlag(entryId, elm, flagged); },
        toFocus: "#txtHistFlag"
      };
    }
    // UNFLAG command
    else {
      // Prepare modal window content
      var bodyHtml = zdSnippets["history.flagEntry"];
      bodyHtml = bodyHtml.replace("{{hint}}", zdPage.ui("history.unflagEntry", "hint"));
      var params = {
        id: "dlgHistFlag",
        title: zdPage.ui("history.unflagEntry", "title"),
        body: bodyHtml,
        confirmed: function () { return onSubmitFlag(entryId, elm, flagged); },
        toFocus: "#txtHistFlag"
      };
    }
    // Show
    zdPage.showModal(params);
    if (flagged) $("#dlgHistFlag i.fa").addClass("unflag");
    else $("#dlgHistFlag i.fa").addClass("flag");
    evt.stopPropagation();
  }

  function onSubmitFlag(entryId, elm, flagged) {
    var cmt = $("#txtHistFlag").val();
    if (cmt.length == 0) {
      return false;
    }
    var statusChage = flagged ? "unflag" : "flag";
    var req = zdAuth.ajax("/api/edit/commententry", "POST", { entryId: entryId, note: cmt, statusChange: statusChage });
    zdPage.setModalWorking("#dlgHistFlag", true);
    req.done(function (data) {
      zdPage.closeModal("dlgHistFlag");
      if (data && data === true) {
        if (flagged)
          zdPage.showAlert(zdPage.ui("history.unflagEntry", "successtitle"), uiStrings["history.unflagEntry"]["successmessage"], false);
        else
          zdPage.showAlert(zdPage.ui("history.flagEntry", "successtitle"), uiStrings["history.flagEntry"]["successmessage"], false);
        reloadItem(entryId, elm);
      }
      else {
        if (flagged)
          zdPage.showAlert(zdPage.ui("history.unflagEntry", "failtitle"), uiStrings["history.unflagEntry"]["failmessage"], true);
        else
          zdPage.showAlert(zdPage.ui("history.flagEntry", "failtitle"), uiStrings["history.flagEntry"]["failmessage"], true);
      }
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.closeModal("dlgHistFlag");
      if (flagged)
        zdPage.showAlert(zdPage.ui("history.unflagEntry", "failtitle"), uiStrings["history.unflagEntry"]["failmessage"], true);
      else
        zdPage.showAlert(zdPage.ui("history.flagEntry", "failtitle"), uiStrings["history.flagEntry"]["failmessage"], true);
    });
    return true;
  }

  function onComment(evt) {
    // Pester to log in
    if (!zdAuth.isLoggedIn()) {
      zdAuth.showLogin(zdPage.ui("history", "loginToEdit"));
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
      confirmed: function () { return onSubmitComment(entryId, elm); },
      toFocus: "#txtHistComment"
    };
    // Show
    zdPage.showModal(params);
    evt.stopPropagation();
  }

  function onSubmitComment(entryId, elm) {
    var cmt = $("#txtHistComment").val();
    if (cmt.length == 0) {
      return false;
    }
    var req = zdAuth.ajax("/api/edit/commententry", "POST", { entryId: entryId, note: cmt, statusChange: "none" });
    zdPage.setModalWorking("#dlgHistComment", true);
    req.done(function (data) {
      zdPage.closeModal("txtHistComment");
      if (data && data === true) {
        zdPage.showAlert(zdPage.ui("history.addComment", "successtitle"), uiStrings["history.addComment"]["successmessage"], false);
        reloadItem(entryId, elm);
      }
      else
        zdPage.showAlert(zdPage.ui("history.addComment", "failtitle"), uiStrings["history.addComment"]["failmessage"], true);
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.closeModal("txtHistComment");
      zdPage.showAlert(zdPage.ui("history.addComment", "failtitle"), uiStrings["history.addComment"]["failmessage"], true);
    });
    return true;
  }

  function reloadItem(entryId, elm) {
    var req = zdAuth.ajax("/api/edit/gethistoryitem", "GET", { entryId: entryId, lang: zdPage.getLang() });
    req.done(function (data) {
      if (!data) return;
      elm.replaceWith(data);
      $("div.pastChanges[data-entry-id='" + entryId + "']").remove();
      // Flash-down effect
      setTimeout(function () {
        $(".historyItem.reloaded").addClass("flashdown");
        // Do exactly the same as in init()!
        // Infuse context into displayed entries
        $(".flashdown .entry").addClass("history");
        // Add tooltips to pliant per-entry commands
        $(".flashdown .opHistComment").tooltipster({
          content: $("<span>" + zdPage.ui("history", "tooltip-comment") + "</span>"),
          position: 'left'
        });
        $(".flashdown .opHistEdit").tooltipster({
          content: $("<span>" + zdPage.ui("history", "tooltip-edit") + "</span>"),
          position: 'left'
        });
        $(".flashdown .opHistFlag").tooltipster({
          content: $("<span>" + zdPage.ui("history", "tooltip-flag") + "</span>"),
          position: 'left'
        });
        $(".flashdown .revealPast").tooltipster({
          content: $("<span>" + zdPage.ui("history", "tooltip-revealpast") + "</span>"),
          position: 'top'
        });
        // In mobile, disable those tooltips
        if (zdPage.isMobile()) {
          $(".flashdown .opHistComment").tooltipster("disable");
          $(".flashdown .opHistEdit").tooltipster("disable");
          $(".flashdown .opHistFlag").tooltipster("disable");
          $(".flashdown .revealPast").tooltipster("disable");
        }
        // Event handlers for per-entry commands
        $(".flashdown .opHistComment").click(onComment);
        //$(".flashdown .opHistEdit").click(onEdit);
        $(".flashdown .opHistFlag").click(onFlag);
        $(".flashdown .revealPast").click(onRevealPast);
        // Touch: hover simulation
        if (zdPage.isTouch()) {
          $(".flashdown .historyItem").bind("touchstart", function (e) {
            $(".historyItem").removeClass("tapOver");
            $(this).addClass("tapOver");
          });
        }
      }, 50);
      setTimeout(function () {
        $(".historyItem.reloaded").removeClass("flashdown");
        $(".historyItem.reloaded").removeClass("reloaded");
      }, 3050);
    });
  }

})();
