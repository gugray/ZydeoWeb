/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdNewEntry = (function () {
  "use strict";

  var reqId = 0;

  $(document).ready(function () {
    zdPage.registerInitScript("edit/new", init);
  });

  function init() {
    // Not logged in: only login "link" logic to set up.
    if ($("#newEntry").length == 0) {
      $("#loginLink").click(function (evt) {
        zdAuth.showLogin(null, function () {
          zdPage.reload();
        });
        evt.stopPropagation();
      });
      return;
    }

    // Label for target input field depends on mutation
    if ($("body").hasClass("hdd")) $("#lblTargetBlock").text(zdPage.ui("newEntry", "target-hdd"));
    else $("#lblTargetBlock").text(zdPage.ui("newEntry", "target-chd"));

    $("#newEntrySimp").bind("compositionstart", onSimpCompStart);
    $("#newEntrySimp").bind("compositionend", onSimpCompEnd);
    $("#newEntrySimp").bind("input", onSimpChanged);
    $("#newEntrySimp").keyup(onSimpKeyUp);
    $("#acceptSimp").click(onSimpAccept);
    $("#editSimp").click(onSimpEdit);
    $("#acceptTrad").click(onTradAccept);
    $("#editTrad").click(onTradEdit);
    $("#acceptPinyin").click(onPinyinAccept);
    $("#editPinyin").click(onPinyinEdit);
    $("#acceptTrg").click(onTrgAccept);
    $("#editTrg").click(onTrgEdit);
    $("#newEntrySubmit").click(onSubmit);

    $("#newEntrySimp").prop("readonly", false);
    $("#newEntrySimp").focus();
  }

  function setActive(block) {
    $(".formBlock").removeClass("active");
    $(".formBlock").removeClass("ready");
    $(".formBlock").removeClass("future");
    $("#blockRefs").addClass("hidden");
    $("#blockReview").addClass("hidden");

    $("#newEntrySimp").prop("readonly", true);
    $("#newEntryTrg").prop("readonly", true);
    $("#newEntryNote").prop("readonly", true);
    $(".formErrors").removeClass("visible");
    $(".formNote").removeClass("hidden");
    if (block == "simp") {
      $("#newEntrySimp").prop("readonly", false);
      $("#newEntrySimp").focus();
      $("#blockSimp").addClass("active");
      $("#blockTrad").addClass("future");
      $("#blockPinyin").addClass("future");
      $("#blockTrg").removeClass("hidden");
      $("#blockExisting").addClass("hidden");
      $("#blockTrg").addClass("future");
      $("#blockRefs").addClass("future");
      $("#blockReview").addClass("future");
      $("#editTrad").removeClass("hidden");
      $("#editPinyin").removeClass("hidden");
    }
    else if (block == "trad") {
      $("#blockSimp").addClass("ready");
      $("#blockTrad").addClass("active");
      $("#blockPinyin").addClass("future");
      $("#blockTrg").removeClass("hidden");
      $("#blockExisting").addClass("hidden");
      $("#blockTrg").addClass("future");
      $("#blockRefs").addClass("future");
      $("#blockReview").addClass("future");
      $("#editPinyin").removeClass("hidden");
    }
    else if (block == "pinyin") {
      $("#blockSimp").addClass("ready");
      $("#blockTrad").addClass("ready");
      $("#blockPinyin").addClass("active");
      $("#blockTrg").removeClass("hidden");
      $("#blockExisting").addClass("hidden");
      $("#blockTrg").addClass("future");
      $("#blockRefs").addClass("future");
      $("#blockReview").addClass("future");
    }
    else if (block == "trg") {
      $("#newEntryTrg").prop("readonly", false);
      $("#newEntryTrg").focus();
      $("#blockSimp").addClass("ready");
      $("#blockTrad").addClass("ready");
      $("#blockPinyin").addClass("ready");
      $("#blockTrg").addClass("active");
      $("#blockRefs").addClass("active");
      $("#blockReview").addClass("future");
    }
    else if (block == "review") {
      $("#blockReview").removeClass("hidden");
      $("#newEntryNote").prop("readonly", false);
      $("#newEntryNote").focus();
      $("#blockSimp").addClass("ready");
      $("#blockTrad").addClass("ready");
      $("#blockPinyin").addClass("ready");
      $("#blockTrg").addClass("ready");
      $("#blockReview").addClass("active");
    }
  }

  // Event handler: submit button clicked.
  function onSubmit(evt) {
    if ($("#newEntrySubmit").hasClass("disabled")) return;
    // Check if user has entered a substantial note
    if ($("#newEntryNote").val().length == 0) {
      $(".formErrors").removeClass("visible");
      $("#errorsReview").addClass("visible");
      $("#newEntryNote").focus();
      return;
    }
    $("#errorsReview").removeClass("visible");
    $("#newEntrySubmit").addClass("disabled");
    var req = zdAuth.ajax("/api/edit/createentry", "POST", {
      simp: $("#newEntrySimp").val(),
      trad: getTrad(),
      pinyin: getPinyin(),
      trg: $("#newEntryTrg").val(),
      note: $("#newEntryNote").val()
    });
    req.done(function (data) {
      $("#newEntrySubmit").removeClass("disabled");
      onSubmitReady(data.success);
    });
    req.fail(function (jqXHR, textStatus, error) {
      $("#newEntrySubmit").removeClass("disabled");
      onSubmitReady(false);
    });
    $("#newEntrySubmit").addClass("disabled");
  }

  // API callback: submit returned, with either success or failure.
  function onSubmitReady(success) {
    if (success && success == true) {
      zdPage.showAlert(zdPage.ui("newEntry", "successCaption"), zdPage.ui("newEntry", "successMsg"), false);
      zdPage.reload();
    }
    else zdPage.showAlert(zdPage.ui("newEntry", "failCaption"), zdPage.ui("newEntry", "failMsg"), true);
  }

  // Event handler: user clicked pencil to continue editing target
  function onTrgEdit(evt) {
    setActive("trg");
    $("#blockRefs").removeClass("hidden");
  }

  // Event handler: user clicked green button to accept translation
  function onTrgAccept(evt) {
    if ($("#acceptTrg").hasClass("disabled")) return;
    var req = zdAuth.ajax("/api/newentry/verifyfull", "GET", {
      simp: $("#newEntrySimp").val(),
      trad: getTrad(),
      pinyin: getPinyin(),
      trg: $("#newEntryTrg").val()
    });
    req.done(function (data) {
      $("#acceptTrg").removeClass("disabled");
      onTrgVerified(data);
    });
    req.fail(function (jqXHR, textStatus, error) {
      $("#acceptTrg").removeClass("disabled");
      zdPage.showAlert(zdPage.ui("newEntry", "verifyFailCaption"), zdPage.ui("newEntry", "verifyFailMsg"), true);
    });
    $("#acceptTrg").addClass("disabled");
  }

  // API callback: translation verified; we might have a preview
  function onTrgVerified(res) {
    if (!res.passed) {
      $(".formErrors").removeClass("visible");
      $("#errorsTrg").addClass("visible");
      $("#noteTrg").addClass("hidden");
      $("#errorListTrg").empty();
      for (var i = 0; i < res.errors.length; ++i) {
        var liErr = $('<li/>');
        liErr.text(res.errors[i]);
        $("#errorListTrg").append(liErr);
      }
      $("#newEntryTrg").focus();
    }
    else {
      $("#errorsTrg").removeClass("visible");
      $("#noteTrg").removeClass("hidden");
      $("#newEntryRender").html(res.preview);
      setActive("review");
    }
  }

  // Even handler: user accepts content of pinyin field
  function onPinyinAccept(evt) {
    if ($("#acceptPinyin").hasClass("disabled")) return;
    var req = zdAuth.ajax("/api/newentry/verifyhead", "GET", {
      simp: $("#newEntrySimp").val(),
      trad: getTrad(),
      pinyin: getPinyin()
    });
    req.done(function (data) {
      $("#acceptPinyin").removeClass("disabled");
      onHeadVerified(data);
    });
    req.fail(function (jqXHR, textStatus, error) {
      $("#acceptPinyin").removeClass("disabled");
      zdPage.showAlert(zdPage.ui("newEntry", "verifyFailCaption"), zdPage.ui("newEntry", "verifyFailMsg"), true);
    });
    $("#acceptPinyin").addClass("disabled");
  }

  // API callback: entire headword has been verified (is it a duplicate?).
  function onHeadVerified(res) {
    if (res.duplicate) {
      $(".formErrors").removeClass("visible");
      $("#errorsPinyin").addClass("visible");
      $("#notePinyin").addClass("hidden");
      $("#blockRefs").addClass("hidden");
      $("#blockTrg").addClass("hidden");
      $("#blockExisting").removeClass("hidden");
      $("#existingEntryRender").html(res.existingEntry);
      var hrefEdit = "/" + zdPage.getLang() + "/edit/existing/" + res.existingEntryId;
      $("#lnkEditExisting").attr("href", hrefEdit);
    }
    else {
      $("#errorsPinyin").removeClass("visible");
      $("#notePinyin").removeClass("hidden");
      $("#blockTrg").removeClass("hidden");
      $("#blockExisting").addClass("hidden");
      setActive("trg");
      // References only shown in CHDICT
      if ($("body").hasClass("chd")) {
        $("#blockRefs").removeClass("hidden");
        $("#newEntryRefEntries").html(res.refEntries);
      }
    }
  }

  // Event handler: user clicks pencil to return to editing pinyin field.
  function onPinyinEdit(evt) {
    setActive("pinyin");
  }

  // Event handler: user clicks pencil to return to editing traditional field.
  function onTradEdit(evt) {
    setActive("trad");
  }

  // Event handler: traditional field is accepted by user.
  function onTradAccept(evt) {
    if ($("#acceptTrad").hasClass("disabled")) return;
    if (isPinyinUnambiguous()) {
      $("#editPinyin").addClass("hidden");
      // Instead of activating target, let's get headword verified
      onPinyinAccept();
    }
    else {
      $("#editPinyin").removeClass("hidden");
      setActive("pinyin");
    }
  }

  // Simplified field is composing (IME). Blocks API calls while field has shadow text.
  var simpComposing = false;

  // Event handler: IME composition starts in simplified field.
  function onSimpCompStart(evt) {
    simpComposing = true;
  }

  // Event handler: IME composition ends in simplified field.
  function onSimpCompEnd(evt) {
    simpComposing = false;
  }

  function onSimpKeyUp(evt) {
    if (evt.which == 13) {
      evt.preventDefault();
      onSimpAccept();
      return false;
    }
  }

  // Handles change of simplified field. Invokes server to retrieve data for subsequent HW fields.
  function onSimpChanged(evt) {
    if (simpComposing) return;
    var simp = $("#newEntrySimp").val();
    if (simp.length == 0) return;
    ++reqId;
    var id = reqId;
    var req = zdAuth.ajax("/api/newentry/processsimp", "GET", { simp: simp });
    req.done(function (data) {
      if (id != reqId) return;
      onSimpProcessed(data.trad, data.pinyin, data.is_known_headword);
    });
    // Here: we silently swallow "fail"
  }

  // Callback when API finished processing current content of simplified field.
  function onSimpProcessed(trad, pinyin, known_hw) {
    $("#newEntryTradCtrl").empty();
    for (var  i = 0; i < trad.length; ++i) {
      var tpos = $('<div class="newEntryTradPos"/>');
      for (var j = 0; j < trad[i].length; ++j) {
        var tspan = $('<span />');
        if (j != 0) tspan.addClass("tradAlt");
        tspan.text(trad[i][j]);
        tpos.append(tspan);
      }
      $("#newEntryTradCtrl").append(tpos);
    }
    if (trad.length == 0) $("#newEntryTradCtrl").append('\xA0');
    if (known_hw) $(".newEntryKnown").addClass("visible");
    else $(".newEntryKnown").removeClass("visible");
    $(".tradAlt").unbind("click", onTradAltClicked);
    $(".tradAlt").click(onTradAltClicked);

    updatePinyin(pinyin);
  }

  // Handles simplified's "accept" event. Invokes server to check input.
  function onSimpAccept(evt) {
    if ($("#acceptSimp").hasClass("disabled")) return;
    var req = zdAuth.ajax("/api/newentry/verifysimp", "GET", { simp: $("#newEntrySimp").val(), lang: zdPage.getLang() });
    req.done(function (data) {
      $("#acceptSimp").removeClass("disabled");
      onSimpVerified(data);
    });
    req.fail(function (jqXHR, textStatus, error) {
      $("#acceptSimp").removeClass("disabled");
      zdPage.showAlert(zdPage.ui("newEntry", "verifyFailCaption"), zdPage.ui("newEntry", "verifyFailMsg"), true);
    });
    $("#acceptSimp").addClass("disabled");
  }

  // Callback when API finished checking simplified.
  // We show error notice, or move on to next field.
  function onSimpVerified(res) {
    // Simplified is not OK - show error
    if (!res.passed) {
      $(".formErrors").removeClass("visible");
      $("#errorsSimp").addClass("visible");
      $("#noteSimp").addClass("hidden");
      $("#errorListSimp").empty();
      for (var i = 0; i < res.errors.length; ++i) {
        var liErr = $('<li/>');
        liErr.text(res.errors[i]);
        $("#errorListSimp").append(liErr);
      }
      $("#newEntrySimp").focus();
    }
    // We're good to go
    else {
      $("#errorsSimp").removeClass("visible");
      $("#noteSimp").removeClass("hidden");
      // If traditional, or even pinyin, are unambiguous: skip ahead one or two steps
      if (isTradUnambiguous()) {
        if (isPinyinUnambiguous()) {
          $("#editTrad").addClass("hidden");
          $("#editPinyin").addClass("hidden");
          // Instead of activating target, let's get headword verified
          onPinyinAccept();
        }
        else {
          $("#editPinyin").removeClass("hidden");
          $("#editTrad").addClass("hidden");
          setActive("pinyin");
        }
      }
      else {
        $("#editPinyin").removeClass("hidden");
        $("#editTrad").removeClass("hidden");
        setActive("trad");
      }
    }
  }

  // Checks if all traditional symbols are unambiguous (no user input needed).
  function isTradUnambiguous() {
    var unambiguous = true;
    var tctrl = $("#newEntryTradCtrl");
    tctrl.children().each(function () {
      if ($(this).children().length > 1) unambiguous = false;
    });
    return unambiguous;
  }

  // Checks if all pinyin syllables are unambiguous (no user input needed).
  function isPinyinUnambiguous() {
    var unambiguous = true;
    var tctrl = $("#newEntryPinyinCtrl");
    tctrl.children().each(function () {
      if ($(this).children().length > 1) unambiguous = false;
    });
    return unambiguous;
  }

  // Even handler: user clicked pencil to edit simplified field.
  function onSimpEdit(evt) {
    setActive("simp");
  }

  // Get user's choice of traditional HW.
  function getTrad() {
    var res = "";
    var tctrl = $("#newEntryTradCtrl");
    tctrl.children().each(function() {
      res += $(this).children().first().text();
    });
    return res;
  }

  // Get user's choice of pinyin in HW.
  function getPinyin() {
    var res = "";
    var tctrl = $("#newEntryPinyinCtrl");
    var first = true;
    tctrl.children().each(function () {
      if (!first) res += " ";
      first = false;
      res += $(this).children().first().text();
    });
    return res;
  }

  // Even handler: user clicked on a non-first-row traditional character to select it.
  function onTradAltClicked(evt) {
    if (!$("#blockTrad").hasClass("active")) return;

    var parent = $(this).parent();
    var tchars = [];
    tchars.push($(this).text());
    parent.children().each(function() {
      if ($(this).text() != tchars[0])
        tchars.push($(this).text());
    });
    parent.empty();
    for (var i = 0; i < tchars.length; ++i) {
      var tspan = $('<span />');
      if (i != 0) tspan.addClass("tradAlt");
      tspan.text(tchars[i]);
      parent.append(tspan);
    }
    $(".tradAlt").unbind("click", onTradAltClicked);
    $(".tradAlt").click(onTradAltClicked);

    ++reqId;
    var id = reqId;
    var req = zdAuth.ajax("/api/newentry/processsimptrad", "GET", { simp: $("#newEntrySimp").val(), trad: getTrad() });
    req.done(function (data) {
      if (id != reqId) return;
      onSimpTradProcessed(data.pinyin, data.isKnownHeadword);
    });
    // Here we simply swallow failure
  }

  // Update data shown in pinyin field.
  function updatePinyin(pinyin) {
    $("#newEntryPinyinCtrl").empty();
    for (var i = 0; i != pinyin.length; ++i) {
      var ppos = $('<div class="newEntryPinyinPos"/>');
      for (var j = 0; j != pinyin[i].length; ++j) {
        var pspan = $('<span/>');
        if (j != 0) pspan.addClass("pyAlt");
        pspan.text(pinyin[i][j]);
        ppos.append(pspan);
      }
      $("#newEntryPinyinCtrl").append(ppos);
    }
    if (pinyin.length == 0) $("#newEntryPinyinCtrl").append('\xA0');
    $(".pyAlt").unbind("click", onPyAltClicked);
    $(".pyAlt").click(onPyAltClicked);
  }

  // Event handler: user clicked a pinyin alternative to select it.
  function onPyAltClicked(evt) {
    if (!$("#blockPinyin").hasClass("active")) return;

    var parent = $(this).parent();
    var tsylls = [];
    tsylls.push($(this).text());
    parent.children().each(function() {
      if ($(this).text() != tsylls[0])
        tsylls.push($(this).text());
    });
    parent.empty();
    for (var i = 0; i < tsylls.length; ++i) {
      var tspan = $('<span />');
      if (i != 0) tspan.addClass("pyAlt");
      tspan.text(tsylls[i]);
      parent.append(tspan);
    }
    $(".pyAlt").unbind("click", onPyAltClicked);
    $(".pyAlt").click(onPyAltClicked);
  }

  // API callback: server finished processing simplified+traditional.
  function onSimpTradProcessed(pinyin, known_hw) {
    updatePinyin(pinyin);
    if (known_hw) $(".newEntryKnown").addClass("visible");
    else $(".newEntryKnown").removeClass("visible");
  }
})();
