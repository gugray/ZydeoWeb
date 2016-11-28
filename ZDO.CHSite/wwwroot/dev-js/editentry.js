/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="../lib/jquery.color-2.1.2.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="strings.en.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdEditEntry = (function () {
  "use strict";

  zdPage.registerInitScript("edit/existing", init);

  var headTxt;
  var trgCurrVal = "";

  function init() {
    // We have one job: get data about entry
    var data = {
      lang: zdPage.getLang(),
      entryId: $(".editexisting").data("entry-id")
    };
    var req = zdAuth.ajax("/api/edit/geteditentrydata", "GET", data);
    req.done(function (res) {
      onGotData(res);
    });
    req.fail(function () {
      zdPage.applyFailHtml();
    });
  }

  function onGotData(data) {
    $(".entry").replaceWith(data.entryHtml);
    headTxt = data.headTxt;
    $("#txtEditTrg").val(data.trgTxt);
    trgCurrVal = $("#txtEditTrg").val();
    if (!data.canApprove) $(".cmdApprove").addClass("disabled");
    if (data.status == 2) $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-unflag"));
    else $(".cmdFlag").text(zdPage.ui("editExisting", "cmd-flag"));
    $(".editexisting").addClass("visible");
    $(".cmdEdit").click(function () {
      $(".pnlTasks").removeClass("visible");
      $(".pnlEdit").addClass("visible");
    });
    // Edit panel wireup
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
          $(".senses").addClass("new");
          $(".previewUpdateFail").removeClass("visible");
        }
        else $(".previewUpdateFail").addClass("visible");
      });
      req.fail(function () {
        $(".previewUpdateFail").addClass("visible");
      })
    });
  }

})();
