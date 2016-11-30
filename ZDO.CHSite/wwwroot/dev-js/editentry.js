/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/jquery.color-2.1.2.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="strings.en.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdEditEntry = (function () {
  "use strict";

  zdPage.registerInitScript("edit/existing", init);

  var origEntryHtml;
  var headTxt;
  var trgOrig;
  var trgCurrVal = "";

  function init() {
    // We have one job: get data about entry
    // ...and wire up events once it's arrived.
    requestData(true);
  }

  function requestData(eventWireup) {
    var data = {
      lang: zdPage.getLang(),
      entryId: $(".editexisting").data("entry-id")
    };
    var req = zdAuth.ajax("/api/edit/geteditentrydata", "GET", data);
    req.done(function (res) {
      onGotData(res, eventWireup);
    });
    req.fail(function () {
      zdPage.applyFailHtml();
    });
  }

  function onGotData(data, eventWireup) {
    origEntryHtml = data.entryHtml;
    $(".entry").replaceWith(origEntryHtml);
    $(".pastChanges").replaceWith(data.historyHtml);
    headTxt = data.headTxt;
    trgOrig = data.trgTxt;
    $("#txtEditTrg").val(trgOrig);
    trgCurrVal = $("#txtEditTrg").val();
    if (!data.canApprove) $(".cmdApprove").addClass("disabled");
    if (data.status == 2) {
      $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-unflag"));
      $(".cmdFlag").removeClass("flag");
      $(".cmdFlag").addClass("unflag");
    }
    else {
      $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-flag"));
      $(".cmdFlag").removeClass("unflag");
      $(".cmdFlag").addClass("flag");
    }
    $(".editexisting").addClass("visible");

    // Need to wire up events too? First time only.
    if (!eventWireup) return;

    // Task panel commands
    $(".pnlTasks .command").click(function (evt) {
      if ($(this).hasClass("disabled")) return;
      // Pester user with login
      if (!zdAuth.isLoggedIn()) {
        zdAuth.showLogin(zdPage.ui("editExisting", "loginToEdit"));
        evt.stopPropagation();
        return;
      }
      // Show action panel
      $(".pnlTasks").removeClass("visible");
      $(".pnlAction").addClass("visible");
      // Disable/enable the right components
      $(".actionTitle").removeClass("visible");
      $("#txtEditTrg").removeClass("visible");
      // Edit entry
      if ($(this).hasClass("cmdEdit")) {
        $("#txtEditTrg").addClass("visible");
        $("#txtEditTrg").focus();
      }
      // NOT edit entry :)
      else {
        $("#txtEditCmt").focus();
        // Comment
        if ($(this).hasClass("cmdComment")) $(".actionTitle.comment").addClass("visible");
        // Flag OR unflag
        else if ($(this).hasClass("cmdFlag")) {
          if ($(this).hasClass("unflag")) $(".actionTitle.unflag").addClass("visible");
          else $(".actionTitle.flag").addClass("visible");
        }
        // Approve
        else if ($(this).hasClass("cmdApprove")) $(".actionTitle.approve").addClass("visible");
      }
    });
    // Wire up different command panels
    actionPanelWireup();
  }

  function actionPanelWireup() {
    // Update entry preview on text change
    $("#txtEditTrg").on("change keyup paste", function () {
      var newVal = $(this).val();
      if (newVal == trgCurrVal) return; //check to prevent multiple simultaneous triggers
      trgCurrVal = newVal;
      // Request entry preview
      var data = {
        hw: headTxt,
        trgTxt: newVal,
        lang: zdPage.getLang()
      };
      var req = zdAuth.ajax("/api/edit/getentrypreview", "GET", data);
      req.done(function (res) {
        if (res) {
          $(".entry").replaceWith(res);
          if (newVal != trgOrig) $(".entry .senses").addClass("new");
          else $(".entry .senses").removeClass("new");
          $(".previewUpdateFail").removeClass("visible");
        }
        else $(".previewUpdateFail").addClass("visible");
      });
      req.fail(function () {
        $(".previewUpdateFail").addClass("visible");
      })
    });
    // Cancel button clicked
    $(".pnlAction .cmdCancel").click(function () {
      exitCommandPanel();
    });
    // Save button clicked
    $(".pnlAction .cmdOK").click(function () {
      // No comment? Pester user.
      if ($("#txtEditCmt").val().trim() == "") {
        $(".errNoComment").addClass("visible");
        return;
      }
      else $(".errNoComment").removeClass("visible");
      // OK, got comment: submit request, depending on which mode we're in
      if ($("#txtEditTrg").hasClass("visible")) onSubmitEdit();
      else onSubmitComment();
    });
  }

  // Comment, flag, approe
  function onSubmitComment() {
    // Only comment, or status change too?
    var statusChange = "none";
    if ($(".actionTitle.flag").hasClass("visible")) statusChange = "flag";
    if ($(".actionTitle.unflag").hasClass("visible")) statusChange = "unflag";
    if ($(".actionTitle.approve").hasClass("visible")) statusChange = "approve";
    var data = {
      entryId: $(".editexisting").data("entry-id"),
      note: $("#txtEditCmt").val(),
      statusChange: statusChange
    };
    var req = zdAuth.ajax("/api/edit/commententry", "POST", data);
    req.done(function (data) {
      if (!data || data != true) {
        zdPage.showAlert(zdPage.ui("editExisting", "failCaption"), zdPage.ui("editExisting", "failMessage"), true);
        return;
      }
      // Yippie.
      exitCommandPanel();
      // Reload data
      requestData(false);
    });
    req.fail(function (jqXHR, textStatus, error) {
      zdPage.showAlert(zdPage.ui("editExisting", "failCaption"), zdPage.ui("editExisting", "failMessage"), true);
    });
  }

  function onSubmitEdit() {
    // Edited target
    var newTrg = $("#txtEditTrg").val();
    // No API call if edited target is actually unchanged
    if (newTrg == trgOrig) {
      exitCommandPanel();
      return;
    }
    // Submit edit
    var data = {
      entryId: $(".editexisting").data("entry-id"),
      trg: newTrg,
      note: $("#txtEditCmt").val()
    };
    var req = zdAuth.ajax("/api/edit/saveentrytrg", "POST", data);
    req.done(function (res) {
      // Server didn't return expected "true"
      if (!res || res != true) {
        zdPage.showAlert(zdPage.ui("editExisting", "failCaption"), zdPage.ui("editExisting", "failMessage"), true);
        return;
      }
      // Yippie.
      exitCommandPanel();
      // Reload data
      requestData(false);
    });
    req.fail(function () {
      zdPage.showAlert(zdPage.ui("editExisting", "failCaption"), zdPage.ui("editExisting", "failMessage"), true);
    });
  }

  function exitCommandPanel() {
    $(".entry .senses").removeClass("new");
    $(".entry").replaceWith(origEntryHtml);
    $(".cmdpanel").removeClass("visible");
    $(".pnlTasks").addClass("visible");
    $("#txtEditTrg").val(trgOrig);
    $("#txtEditCmt").val("");
  }

})();
