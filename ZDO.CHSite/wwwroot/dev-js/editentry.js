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
  var canApprove;
  var simpOrig;
  var tradOrig;
  var pinyinOrig;
  var trgOrig;
  var simpCurrVal = "";
  var tradCurrVal = "";
  var pinyinCurrVal = "";
  var trgCurrVal = "";

  function init() {
    // Placholder page (no entry in URL)
    var entryId = $(".editexisting").data("entry-id")
    if (!entryId) return;

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
    canApprove = data.canApprove;
    $(".entry").replaceWith(origEntryHtml);
    $(".entry").addClass("editentry");
    $(".pastChanges").replaceWith(data.historyHtml);
    simpOrig = data.headSimp;
    tradOrig = data.headTrad;
    pinyinOrig = data.headPinyin;
    trgOrig = data.trgTxt;
    $("#txtEditSimp").val(simpOrig);
    $("#txtEditTrad").val(tradOrig);
    $("#txtEditPinyin").val(pinyinOrig);
    $("#txtEditTrg").val(trgOrig);
    simpCurrVal = $("#txtEditSimp").val();
    tradCurrVal = $("#txtEditTrad").val();
    pinyinCurrVal = $("#txtEditPinyin").val();
    trgCurrVal = $("#txtEditTrg").val();
    // Approve disabled if entry is already approved
    if (data.status == "approved") $(".cmdApprove").addClass("disabled");
    else $(".cmdApprove").removeClass("disabled");
    // Flagged: text and meaning of Flag button
    if (data.status == "flagged") {
      $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-unflag"));
      $(".cmdFlag").removeClass("flag");
      $(".cmdFlag").addClass("unflag");
    }
    else {
      $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-flag"));
      $(".cmdFlag").removeClass("unflag");
      $(".cmdFlag").addClass("flag");
    }
    // Now reveal page
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

      // Approve entry: check if user is authorized
      if ($(this).hasClass("cmdApprove") && !canApprove) {
        var params = {
          id: "dlgCannotApprove",
          confirmed: function () { return true; },
          title: zdPage.ui("editExisting", "cannotApproveTitle"),
          body: escapeHTML(zdPage.ui("editExisting", "cannotApproveMsg"))
        };
        // Show
        zdPage.showModal(params);
        $("#dlgCannotApprove .modalPopupButtonCancel").addClass("hidden");
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
        $(".actionTitle.edit").addClass("visible");
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

  function entryFieldsChanged(forceUpdate) {
    $(".errSaveFailed").removeClass("visible");
    var newSimp = $("#txtEditSimp").val();
    var newTrad = $("#txtEditTrad").val();
    var newPinyin = $("#txtEditPinyin").val();
    var newTrg = $("#txtEditTrg").val();
    //check to prevent multiple simultaneous triggers
    if (!forceUpdate) {
      if (newSimp == simpCurrVal && newTrad == tradCurrVal && newPinyin == pinyinCurrVal && newTrg == trgCurrVal)
        return;
    }
    simpCurrVal = newSimp;
    tradCurrVal = newTrad;
    pinyinCurrVal = newPinyin;
    trgCurrVal = newTrg;
    // Request entry preview
    var data = {
      origHw: tradOrig + " " + simpOrig + " [" + pinyinOrig + "]",
      trad: tradCurrVal,
      simp: simpCurrVal,
      pinyin: pinyinCurrVal,
      trgTxt: newTrg,
      lang: zdPage.getLang()
    };
    var req = zdAuth.ajax("/api/edit/getentrypreview", "GET", data);
    req.done(function (res) {
      if (res.previewHtml) {
        $(".entry").replaceWith(res.previewHtml);
        // Hilite changes
        if (newSimp != simpOrig) $(".entry .hw-simp").addClass("new");
        else $(".entry .hw-simp").removeClass("new");
        if (newTrad != tradOrig) $(".entry .hw-trad").addClass("new");
        else $(".entry .hw-trad").removeClass("new");
        if (newPinyin != pinyinOrig) $(".entry .hw-pinyin").addClass("new");
        else $(".entry .hw-pinyin").removeClass("new");
        if (newTrg != trgOrig) $(".entry .senses").addClass("new");
        else $(".entry .senses").removeClass("new");
        $(".previewUpdateFail").removeClass("visible");
        updateHwErrors(".errBadSimp", res.errorsSimp);
        updateHwErrors(".errBadTrad", res.errorsTrad);
        updateHwErrors(".errBadPinyin", res.errorsPinyin);
      }
      else $(".previewUpdateFail").addClass("visible");
    });
    req.fail(function () {
      $(".previewUpdateFail").addClass("visible");
    })
  }

  function updateHwErrors(elmClass, errors) {
    if (errors.length > 0) {
      var html = "";
      for (var i = 0; i != errors.length; ++i) {
        var err = errors[i];
        if (err.error) html += "<span class='error'>";
        else html += "<span>";
        html += escapeHTML(err.message);
        html += "</span><br/>";
      }
      $(elmClass).html(html);
      $(elmClass).addClass("visible");
    }
    else { $(elmClass).removeClass("visible"); }
  }

  function actionPanelWireup() {
    // Update entry preview on text change
    $("#txtEditSimp").on("change keyup paste", entryFieldsChanged);
    $("#txtEditTrad").on("change keyup paste", entryFieldsChanged);
    $("#txtEditPinyin").on("change keyup paste", entryFieldsChanged);
    $("#txtEditTrg").on("change keyup paste", entryFieldsChanged);
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
      if ($(".actionTitle.edit").hasClass("visible")) onSubmitEdit();
      else onSubmitComment();
    });
    // Edit headword clicked
    $(".pnlAction .cmdEditHead").click(function () {
      // Show headword fields; remove link command
      $(".headwordFields").addClass("visible");
      $(".cmdEditHead").addClass("hidden");
      // Trigger preview update: that retrieves headword-related warnings too
      entryFieldsChanged(true);
    });
  }

  // Comment, flag, approe
  function onSubmitComment() {
    // Only comment, or status change too?
    var statusChange = "none";
    if ($(".actionTitle.flag").hasClass("visible")) statusChange = "flag";
    if ($(".actionTitle.unflag").hasClass("visible")) statusChange = "unflag";
    if ($(".actionTitle.approve").hasClass("visible")) statusChange = "approve";
    // Prepare request
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
    // Edited values
    var newSimp = $("#txtEditSimp").val();
    var newTrad = $("#txtEditTrad").val();
    var newPinyin = $("#txtEditPinyin").val();
    var newTrg = $("#txtEditTrg").val();
    // No API call if nothing actually unchanged
    var hwChanged = (newSimp != simpOrig || newTrad != tradOrig || newPinyin != pinyinOrig);
    if (!hwChanged && newTrg == trgOrig) {
      exitCommandPanel();
      return;
    }
    // Data to submit
    var data = {
      entryId: $(".editexisting").data("entry-id"),
      trg: newTrg,
      note: $("#txtEditCmt").val(),
      lang: zdPage.getLang()
    };
    var url = "/api/edit/saveentrytrg";
    // Headword changed or not: different requests
    if (hwChanged) {
      data.hw = newTrad + " " + newSimp + " [" + newPinyin + "]";
      url = "/api/edit/savefullentry";
    }
    // Submit
    var req = zdAuth.ajax(url, "POST", data);
    req.done(function (res) {
      // Server failed to save
      if (!res.success) {
        $(".errSaveFailed").text(res.error);
        $(".errSaveFailed").addClass("visible");
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
    $(".headwordFields").removeClass("visible");
    $(".hwError").text("");
    $(".hwError").removeClass("visible");
    $(".cmdEditHead").removeClass("hidden");
    $(".errSaveFailed").text("");
    $(".errSaveFailed").removeClass("visible");
    $("#txtEditSimp").val(simpOrig);
    $("#txtEditTrad").val(tradOrig);
    $("#txtEditPinyin").val(pinyinOrig);
    $("#txtEditTrg").val(trgOrig);
    $("#txtEditCmt").val("");
  }

})();
