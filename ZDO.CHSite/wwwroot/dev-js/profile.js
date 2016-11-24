/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdProfile = (function () {
  "use strict";

  zdPage.registerInitScript("user/profile", init);
  zdPage.registerInitScript("user/confirm", init);

  $(document).ready(function () {
  });

  var publicInfoChanged = false;

  function init() {
    // We're on My profile
    if ($("div.content").hasClass("myprofile")) {
      $(".content .command").click(function (evt) {
        if ($(this).hasClass("changeEmail")) {
          showPopup("changeEmailView", "Change email", changeEmailOK);
          $("#currentPass2").focus();
          evt.stopPropagation();
        }
        else if ($(this).hasClass("changePassword")) {
          showPopup("changePassView", "Change password", changePasswordOK);
          $("#currentPass1").focus();
          evt.stopPropagation();
        }
        else if ($(this).hasClass("editPublicInfo")) {
          publicInfoChanged = false;
          showPopup("editInfoView", "Edit public information", editPublicInfoOK, editPublicInfoClosed);
          evt.stopPropagation();
        }
        else if ($(this).hasClass("deleteProfile")) {
          zdAuth.showDeleteProfile();
          evt.stopPropagation();
        }
      });
    }
    // We're on new email confirmation page
    else if ($("div.content").hasClass("confirmnewemail")) {
      // Log out user, silently
      zdAuth.clientSilentLogout();
    }
    // We're on password reset page
    else if ($("div.content").hasClass("passreset")) {
      // Silently log out user
      zdAuth.clientSilentLogout();
      // Password reveal mechanics
      $("#togglePassVisible").tooltipster({});
      $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipPeekPassword"));
      $("#togglePassVisible").click(function () {
        if ($("#togglePassVisible").hasClass("fa-eye")) {
          $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipHidePassword"));
          $("#togglePassVisible").removeClass("fa-eye");
          $("#togglePassVisible").addClass("fa-eye-slash");
          $("#newPass").attr("type", "text");
        }
        else {
          $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipPeekPassword"));
          $("#togglePassVisible").removeClass("fa-eye-slash");
          $("#togglePassVisible").addClass("fa-eye");
          $("#newPass").attr("type", "password");
        }
      });
      // OK button
      $(".page1 input").keyup(function (e) {
        if (e.keyCode == 13) $(".buttonOK").trigger("click");
      });
      $(".buttonOK").click(submitPassReset);
    }
  }

  function submitPassReset() {
    var newPass = $("#newPass").val();
    if (newPass.length < 6) $(".invalidPass").addClass("visible");
    else $(".invalidPass").removeClass("visible");
    if (newPass.length < 6) return;

    $(".passreset .page").removeClass("visible");
    var code = $(".passreset").data("code");
    var req = zdAuth.ajax("/api/auth/resetpassword", "POST", { pass: newPass, code: code });
    req.done(function (data) {
      if (data && data == true) {
        // Success: finished page
        $(".passreset .pageSuccess").addClass("visible");
      }
      else {
        // Failed: password was wrong, or token expired in the meantime, etc.
        $(".passreset .pageFail").addClass("visible");
      }
    });
    req.fail(function () {
      $(".passreset .pageFail").addClass("visible");
    });
  }

  function showPopup(viewToActivate, title, confirmCallback, closedCallback) {
    var bodyHtml = zdSnippets["profile.popup"];
    //bodyHtml = zdPage.localize("login", bodyHtml);
    var params = {
      id: "dlgProfilePopup",
      title: title,
      body: bodyHtml,
      confirmed: confirmCallback,
      onClosed: closedCallback
    };
    // Show
    zdPage.showModal(params);
    $("#dlgProfilePopup ." + viewToActivate).addClass("visible");
    $("#dlgProfilePopup").addClass("userAccountDialog");
    // Enter key events
    $("#dlgProfilePopup input").keyup(function (e) {
      if (e.keyCode == 13) $(".modalPopupButtonOK").trigger("click");
    });
    $("#togglePassVisible").tooltipster({});
    $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipPeekPassword"));
    $("#togglePassVisible").click(function () {
      if ($("#togglePassVisible").hasClass("fa-eye")) {
        $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipHidePassword"));
        $("#togglePassVisible").removeClass("fa-eye");
        $("#togglePassVisible").addClass("fa-eye-slash");
        $("#newPass").attr("type", "text");
      }
      else {
        $("#togglePassVisible").tooltipster("content", zdPage.ui("login", "tooltipPeekPassword"));
        $("#togglePassVisible").removeClass("fa-eye-slash");
        $("#togglePassVisible").addClass("fa-eye");
        $("#newPass").attr("type", "password");
      }
    });
  }

  function changePasswordOK() {
    // Already final page? OK closes dialog.
    if (!$(".changePassView").hasClass("visible")) return true;

    $(".wrongPass").removeClass("visible");
    var oldPass = $("#currentPass1").val();
    var newPass = $("#newPass").val();
    if (newPass.length < 6) $(".invalidPass").addClass("visible");
    else $(".invalidPass").removeClass("visible");
    if (newPass.length < 6) return false;
    var req = zdAuth.ajax("/api/auth/changepassword", "POST", { oldPass: oldPass, newPass: newPass });
    req.done(function (data) {
      if (data && data == true) {
        // Success: finished page
        $(".dlgInner").removeClass("visible");
        $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
        $(".changePassDoneView").addClass("visible");
      }
      else {
        // Failed: password was wrong
        $(".wrongPass").addClass("visible");
      }
    });
    req.fail(function () {
      $(".dlgInner").removeClass("visible");
      $(".veryWrongView").addClass("visible");
      $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
    });
    return false;
  }

  function changeEmailOK() {
    // Already final page? OK closes dialog.
    if (!$(".changeEmailView").hasClass("visible")) return true;

    $(".wrongPass").removeClass("visible");
    $(".invalidMail").removeClass("visible");
    $(".emailInUse").removeClass("visible");
    if (!zdAuth.isValidEmail($("#newEmail").val())) {
      $(".invalidMail").addClass("visible");
      return false;
    }
    var req = zdAuth.ajax("/api/auth/changeemail", "POST", {
      pass: $("#currentPass2").val(),
      newEmail: $("#newEmail").val(),
      lang: zdPage.getLang()
    });
    req.done(function (data) {
      if (data && data == "ok") {
        // Success: finished page
        $(".dlgInner").removeClass("visible");
        $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
        $(".changeEmailDoneView").addClass("visible");
      }
      else if (data && data == "badpass") {
        // Failed: password was wrong
        $(".wrongPass").addClass("visible");
      }
      else if (data && data == "emailinuse") {
        // Failed: password was wrong
        $(".emailInUse").addClass("visible");
      }
      else {
        // Any other fail: very bad
        $(".dlgInner").removeClass("visible");
        $(".veryWrongView").addClass("visible");
        $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
      }
    });
    req.fail(function () {
      $(".dlgInner").removeClass("visible");
      $(".veryWrongView").addClass("visible");
      $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
    });
    return false;
  }

  function editPublicInfoOK() {
    // Already final page? OK closes dialog.
    if (!$(".editInfoView").hasClass("visible")) return true;

    var req = zdAuth.ajax("/api/auth/changeinfo", "POST", { location: $("#txtLocation").val(), about: $("#txtAboutMe").val() });
    req.done(function (data) {
      $(".dlgInner").removeClass("visible");
      $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
      $(".editInfoDoneView").addClass("visible");
      publicInfoChanged = true;
    });
    req.fail(function () {
      $(".dlgInner").removeClass("visible");
      $(".veryWrongView").addClass("visible");
      $("#dlgProfilePopup .modalPopupButtonCancel").addClass("hidden");
    });
    return false;
  }

  function editPublicInfoClosed() {
    if (publicInfoChanged) zdPage.reload();
  }

})();
