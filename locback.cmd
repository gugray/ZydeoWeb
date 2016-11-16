@echo off

echo Copying content
copy _locback\*.json	ZDO.CHSite\files\strings\
copy _locback\*.js	ZDO.CHSite\wwwroot\dev-js\
copy _locback\*.html	ZDO.CHSite\files\html\

echo Fixing array name in JS
powershell -Command "(gc ZDO.CHSite\wwwroot\dev-js\strings.de.js) -replace 'uiStringsEn', 'uiStringsDe' | Out-File ZDO.CHSite\wwwroot\dev-js\strings.de.js"
powershell -Command "(gc ZDO.CHSite\wwwroot\dev-js\strings.hu.js) -replace 'uiStringsEn', 'uiStringsHu' | Out-File ZDO.CHSite\wwwroot\dev-js\strings.hu.js"
