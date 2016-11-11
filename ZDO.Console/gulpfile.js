/// <binding BeforeBuild='default' Clean='clean' ProjectOpened='watch' />
var gulp = require('gulp');
var less = require('gulp-less');
var path = require('path');
var concat = require('gulp-concat');
var plumber = require('gulp-plumber');
var uglify = require('gulp-uglify');
var minifyCSS = require('gulp-minify-css');
var del = require('del');

// Compile all .less files to .css
gulp.task('less', function () {
  return gulp.src('./wwwroot/dev/*.less')
    .pipe(plumber())
    .pipe(less({
      paths: [path.join(__dirname, 'less', 'includes')]
    })).on('error', function (e) { console.log(e); })
    .pipe(gulp.dest('./wwwroot/dev/'));
});

// Delete all compiled and bundled files
gulp.task('clean', function () {
  return del(['./wwwroot/dev/*.css', './wwwroot/prod/*']);
});

// Minify and bundle JS files
gulp.task('scripts', function () {
  return gulp.src([
    './wwwroot/lib/*.js',
    './wwwroot/dev/page.js'
  ])
    .pipe(uglify().on('error', function (e) { console.log(e); }))
    .pipe(concat('app.min.js'))
    .pipe(gulp.dest('./wwwroot/prod/'));
});

// Minify and bundle CSS files
gulp.task('styles', ['less'], function () {
  return gulp.src(['./wwwroot/dev/*.css', '!./wwwroot/dev/*.min.css'])
    .pipe(minifyCSS())
    .pipe(concat('app.min.css'))
    .pipe(gulp.dest('./wwwroot/prod/'));
});

// Default task: full clean+build.
gulp.task('default', ['clean', 'scripts', 'styles'], function () { });

// Watch: recompile less on changes
gulp.task('watch', function () {
  gulp.watch(['./wwwroot/dev/*.less'], ['less']);
});
