/// <reference path="../lib/jquery-2.1.4.min.js" />

function startsWith(str, prefix) {
  if (str.length < prefix.length)
    return false;
  for (var i = prefix.length - 1; (i >= 0) && (str[i] === prefix[i]) ; --i)
    continue;
  return i < 0;
}

function escapeHTML(s) {
  return s.replace(/&/g, '&amp;')
          .replace(/"/g, '&quot;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;');
}

var thePage = (function () {
  "use strict";

  var rel;

  // Parse full path, language, and relative path from URL
  function parseLocation() {
    var loc = window.history.location || window.location;
    var rePath = /https?:\/\/[^\/]+\/(.*)/i;
    var match = rePath.exec(loc);
    rel = match[1];
  }

  // Page just loaded: time to get dynamic part asynchronously, wherever we just landed
  $(document).ready(function () {
    parseLocation();
    $(".command").click(onCommand);
    $("#btnRefresh").click(function (evt) {
      fetchValues();
      getStatus(true);
      evt.preventDefault();
    });
    fetchValues();
    getStatus();
  });

  function getStatus(clearStatus) {
    var actClearStatus;
    if (clearStatus === undefined || clearStatus === null) actClearStatus = false;
    else actClearStatus = true;
    var req = $.ajax({
      url: "/api/getstatus",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { clearStatus: actClearStatus }
    });
    req.done(function (data) {
      $.each(data, function (key, value) {
        $("span[data-infofield='" + key + "']").text(value);
      });
      $("#msgStatus").text(data.statusMsg);
      $(".statusbar").removeClass("success").removeClass("fail").removeClass("working");
      $(".statusbar").addClass(data.statusClass);
      if (data.statusClass == "working") setTimeout(getStatus, 1000);
    });
    req.fail(function (jqXHR, textStatus, error) {
      $("#msgStatus").text("Error: failed to retrieve console status.");
      $(".statusbar").removeClass("success").removeClass("fail").removeClass("working");
      $(".statusbar").addClass("fail");
    });
  }

  function fetchValues() {
    // Everything except response
    $(".info").text("");
    var req = $.ajax({
      url: "/api/getvalues",
      type: "GET",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { shortName: rel }
    });
    req.done(function (data) {
      $.each(data, function (key, value) {
        $("span[data-infofield='" + key + "']").text(value);
      });
    });
    req.fail(function (jqXHR, textStatus, error) {
      var msg = "Failed to retrieve information from console.";
      if (jqXHR.responseText) msg += "\n" + jqXHR.responseText;
      alert(msg);
    });
    // Response - separately, may time out
  }

  function onCommand() {
    var req = $.ajax({
      url: "/api/execute",
      type: "POST",
      contentType: "application/x-www-form-urlencoded; charset=UTF-8",
      data: { shortName: rel, cmd: $(this).data("command") }
    });
    req.done(function (data) {
      $("#msgStatus").text(data.statusMsg);
      $(".statusbar").removeClass("success").removeClass("fail").removeClass("working");
      $(".statusbar").addClass(data.statusClass);
      fetchValues();
      getStatus();
    });
    req.fail(function (jqXHR, textStatus, error) {
      var msg = "Failed to start console task.";
      if (jqXHR.responseText) msg += "\n" + jqXHR.responseText;
      alert(msg);
      fetchValues();
    });

  }

})();
