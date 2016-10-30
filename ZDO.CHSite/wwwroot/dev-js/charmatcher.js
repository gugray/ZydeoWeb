// Written in 2015 by Shaunak Kishore (kshaunak@gmail.com).
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.

// Modified in 2016 by Gabor L Ugray: wrapped up into zdCharMatcher namespace function for use in ZydeoWeb
// Worked around EC6 features (browser supprt, plus Gulp crash)
// Converted => functions to named ones; flattened out Matcher class; replaced let and const with var.
// No material change to original code beyond these transformations.

var zdCharMatcher = (function () {

  var _params;
  var _medians;

  function distance2(point1, point2) {
    return norm2(subtract(point1, point2));
  }
  function norm2(point) {
    return point[0] * point[0] + point[1] * point[1];
  }
  function round(point) {
    return point.map(Math.round);
  }
  function subtract(point1, point2) {
    return [point1[0] - point2[0], point1[1] - point2[1]];
  }
  function coerce(x, y) {
    return x == null ? y : x;
  }

  function filterMedian(median, n) {
    var result = [];
    var total = 0;
    for (var i = 0; i < median.length - 1; i++) {
      total += Math.sqrt(distance2(median[i], median[i + 1]));
    }
    var index = 0;
    var position = median[0];
    var total_so_far = 0;
    for (var i = 0; i < n - 1; i++) {
      var target = i * total / (n - 1);
      while (total_so_far < target) {
        var step = Math.sqrt(distance2(position, median[index + 1]));
        if (total_so_far + step < target) {
          index += 1;
          position = median[index];
          total_so_far += step;
        } else {
          var t = (target - total_so_far) / step;
          position = [(1 - t) * position[0] + t * median[index + 1][0],
                      (1 - t) * position[1] + t * median[index + 1][1]];
          total_so_far = target;
        }
      }
      result.push(round(position));
    }
    result.push(median[median.length - 1]);
    return result;
  }

  function getAffineTransform(source, target) {
    var sdiff = subtract(source[1], source[0]);
    var tdiff = subtract(target[1], target[0]);
    var ratio = [tdiff[0] / sdiff[0], tdiff[1] / sdiff[1]];
    return function (point) {
      return [
        Math.round(ratio[0] * (point[0] - source[0][0]) + target[0][0]),
        Math.round(ratio[1] * (point[1] - source[0][1]) + target[0][1]),
      ];
    }
  }

  function getBounds(medians) {
    var min = [Infinity, Infinity];
    var max = [-Infinity, -Infinity];
    medians.map(function(median) {
      return median.map(function(point) {
        min[0] = Math.min(min[0], point[0]);
        min[1] = Math.min(min[1], point[1]);
        max[0] = Math.max(max[0], point[0]);
        max[1] = Math.max(max[1], point[1]);
      })
    });
    return [min, max];
  }

  function normalizeBounds(bounds, max_ratio, min_width) {
    bounds = bounds.map(round);
    var diff = subtract(bounds[1], bounds[0]);
    if (diff[0] < 0 || diff[1] < 0) throw diff;
    if (diff[0] < min_width) {
      var extra = Math.ceil((min_width - diff[0]) / 2);
      bounds[0][0] -= extra;
      bounds[1][0] += extra;
    }
    if (diff[1] < min_width) {
      var extra = Math.ceil((min_width - diff[1]) / 2);
      bounds[0][1] -= extra;
      bounds[1][1] += extra;
    }
    if (max_ratio > 0) {
      diff = subtract(bounds[1], bounds[0]);
      if (diff[0] < diff[1] / max_ratio) {
        var extra = Math.ceil((diff[1] / max_ratio - diff[0]) / 2);
        bounds[0][0] -= extra;
        bounds[1][0] += extra;
      } else if (diff[1] < diff[0] / max_ratio) {
        var extra = Math.ceil((diff[0] / max_ratio - diff[1]) / 2);
        bounds[0][1] -= extra;
        bounds[1][1] += extra;
      }
    }
    return bounds;
  }

  function preprocessMedians(medians) {
    if (medians.length === 0 || medians.some(function (median) { return median.length === 0; })) {
      throw new Error('Invalid medians list: ${JSON.stringify(medians)}');
    }

    var n = _params.side_length;
    var source = normalizeBounds(
        getBounds(medians), _params.max_ratio, _params.min_width);
    var target = [[0, 0], [_params.side_length - 1, _params.side_length - 1]];
    var transform = getAffineTransform(source, target);

    return medians.map(function(median) {
      var result = filterMedian(median.map(transform), _params.points);
      var diff = subtract(result[result.length - 1], result[0]);
      var angle = Math.atan2(diff[1], diff[0]);
      var normalized = Math.round((angle + Math.PI) * n / (2 * Math.PI)) % n;
      var length = Math.round(Math.sqrt(norm2(diff) / 2));
      return [].concat.apply([], result).concat([normalized, length]);
    });
  }

  function scoreMatch(source, target, verbose) {
    var score = 0;
    var n = _params.points;
    for (var i = 0; i < source.length; i++) {
      var median1 = source[i];
      var median2 = target[i];
      for (var j = 0; j < n; j++) {
        score -= Math.abs(median1[2 * j] - median2[2 * j]);
        score -= Math.abs(median1[2 * j + 1] - median2[2 * j + 1]);
      }
      var angle = Math.abs(median1[2 * n] - median2[2 * n]);
      var ratio = (median1[2 * n + 1] + median2[2 * n + 1]) / _params.side_length;
      score -= 4 * n * ratio * Math.min(angle, _params.side_length - angle);
    }
    return score;
  }

  return {
    // Initializes matcher with provided character data (medians).
    init: function (medians) {
      params = {};
      params.points = coerce(params.points, 4);
      params.max_ratio = coerce(params.max_ratio, 1);
      params.min_width = coerce(params.max_width, 8);
      params.side_length = coerce(params.side_length, 256);

      _medians = medians;
      _params = params;
    },

    // Returns n best matches for provided hand-drawn medians (strokes)
    match: function (medians, n) {
      if (medians.length === 0) return [];
      n = n || 1;
      var candidates = [];
      var scores = [];
      medians = preprocessMedians(medians);
      for (var mx = 0; mx != _medians.length; ++mx) {
        var entry = _medians[mx];
        if (entry[1].length !== medians.length) {
          continue;
        }
        var score = scoreMatch(medians, entry[1]);
        var i = scores.length;
        while (i > 0 && score > scores[i - 1]) {
          i -= 1;
        }
        if (i < n) {
          candidates.splice(i, 0, entry[0]);
          scores.splice(i, 0, score);
          if (candidates.length > n) {
            candidates.pop();
            scores.pop();
          }
        }
      }
      return candidates;
    }
  }

})();

