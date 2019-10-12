/// <reference path="../lib/jquery-3.4.1.min.js" />
/// <reference path="../lib/jquery.color-2.1.2.min.js" />
/// <reference path="../lib/jquery.tooltipster.min.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />
/// <reference path="handwriting.js" />
/// <reference path="strokeanim.js" />

var zdFaq = (function () {
  "use strict";

  zdPage.registerInitScript("read/about", init);

  function init() {
    $("h3").click(function () {
      var cls = ".faq-section-" + $(this).data("faq-section");
      if ($(cls).hasClass("visible")) $(cls).removeClass("visible");
      else $(cls).addClass("visible");
    });
    $(".faq-expand-all").click(function () {
      $(".faq-section").addClass("visible");
    });
  }
})();
