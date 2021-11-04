/// <binding Clean='clean' ProjectOpened='watch' />
const gulp = require('gulp');
const less = require('gulp-less');
const path = require('path');
const concat = require('gulp-concat');
const plumber = require('gulp-plumber');
const uglify = require('gulp-uglify');
const minifyCSS = require('gulp-minify-css');
const del = require('del');
const htmltojson = require('gulp-html-to-json');

// Compile all .less files to .css
gulp.task('less', function () {
  return gulp.src('./wwwroot/dev-style/*.less')
    .pipe(plumber())
    .pipe(less({
      paths: [path.join(__dirname, 'less', 'includes')]
    }))
    .pipe(gulp.dest('./wwwroot/dev-style/'));
});

// Minify and bundle CSS files
gulp.task('styles', gulp.series('less', function () {
  return gulp.src(['./wwwroot/dev-style/*.css', '!./wwwroot/dev-style/*.min.css'])
    .pipe(minifyCSS())
    .pipe(concat('app.min.css'))
    .pipe(gulp.dest('./wwwroot/prod-style/'));
}));


// Delete all compiled and bundled files
gulp.task('clean', function () {
  return del(['./wwwroot/dev-style/*.css', './wwwroot/prod-style/*', './wwwroot/prod-js/*']);
});

gulp.task('snippets', function () {
  return gulp.src('./wwwroot/dev-snippets/_snippets.js')
    .pipe(htmltojson({
      filename: "zdSnippets",
      useAsVariable: true
    }))
    .pipe(gulp.dest('./wwwroot/dev-js'));
});

// Copies raw scripts (character data) to production folder
gulp.task('scriptcopy', function () {
  return gulp.src(['./wwwroot/dev-js/xcharacterdata.js']).pipe(gulp.dest('./wwwroot/prod-js/'));
});

// Minify and bundle JS files
gulp.task('scripts', gulp.series('snippets', 'scriptcopy', function () {
  return gulp.src([
    './wwwroot/lib/*.js',
    './wwwroot/dev-js/zdSnippets.js',
    './wwwroot/dev-js/strings*.js',
    './wwwroot/dev-js/auth.js',
    './wwwroot/dev-js/handwriting.js',
    './wwwroot/dev-js/page.js',
    './wwwroot/dev-js/newentry.js',
    './wwwroot/dev-js/editentry.js',
    './wwwroot/dev-js/strokeanim.js',
    './wwwroot/dev-js/lookup.js',
    './wwwroot/dev-js/history.js',
    './wwwroot/dev-js/profile.js',
    './wwwroot/dev-js/download.js',
    './wwwroot/dev-js/faq.js'
  ])
    .pipe(uglify().on('error', function (e) { console.log(e); }))
    .pipe(concat('app.min.js'))
    .pipe(gulp.dest('./wwwroot/prod-js/'));
}));

// Default task: full clean+build.
gulp.task('default', gulp.series('clean', 'scripts', 'styles', function (done) { done(); }));

// Watch: recompile less on changes
gulp.task('watch', function () {
  gulp.watch(['./wwwroot/dev-snippets/*.html'], ['snippets']);
  gulp.watch(['./wwwroot/dev-style/*.less'], ['less']);
});
