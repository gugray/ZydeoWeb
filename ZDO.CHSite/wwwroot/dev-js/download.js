/// <reference path="../lib/jquery-2.1.4.min.js" />
/// <reference path="auth.js" />
/// <reference path="page.js" />

var zdHistory = (function () {
  "use strict";

  zdPage.registerInitScript("download", init);

  function init() {
    var req = zdAuth.ajax("/api/export/downloadinfo", "GET", null);
    req.done(function (data) {
      $("#lnkDictDownload").text(data.fileName);
      $("#exportEntryCount").text(data.entryCount);
      $("#exportDate").text(data.timestamp);
    });
  }
})();
