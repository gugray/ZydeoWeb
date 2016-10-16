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
  return gulp.src('./wwwroot/dev-style/*.less')
    .pipe(plumber())
    .pipe(less({
      paths: [path.join(__dirname, 'less', 'includes')]
    }))
    .pipe(gulp.dest('./wwwroot/dev-style/'));
});

// Delete all compiled and bundled files
gulp.task('clean', function () {
  return del(['./wwwroot/dev-style/*.css', './wwwroot/prod-style/*', './wwwroot/prod-js/*']);
});

// Minify and bundle JS files
gulp.task('scripts', function () {
  return gulp.src([
    './wwwroot/lib/*.js',
    './wwwroot/dev-js/strings*.js',
    './wwwroot/dev-js/page.js',
    './wwwroot/dev-js/newentry.js',
    './wwwroot/dev-js/strokeanim.js',
    './wwwroot/dev-js/lookup.js',
    './wwwroot/dev-js/history.js',
    './wwwroot/dev-js/diagnostics.js'
  ])
    .pipe(uglify())
    .pipe(concat('app.min.js'))
    .pipe(gulp.dest('./wwwroot/prod-js/'));
});

// Minify and bundle CSS files
gulp.task('styles', ['less'], function () {
  return gulp.src(['./wwwroot/dev-style/*.css', '!./wwwroot/dev-style/*.min.css'])
    .pipe(minifyCSS())
    .pipe(concat('app.min.css'))
    .pipe(gulp.dest('./wwwroot/prod-style/'));
});

// Default task: full clean+build.
gulp.task('default', ['clean', 'scripts', 'styles'], function () { });

// Watch: recompile less on changes
gulp.task('watch', function () {
  gulp.watch(['./wwwroot/dev-style/*.less'], ['less']);
});
