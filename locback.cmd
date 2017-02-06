@echo off

echo Copying content
copy _locback\*.json		ZDO.CHSite\files\strings\
copy _locback\*.js		ZDO.CHSite\wwwroot\dev-js\
copy _locback\*.html		ZDO.CHSite\files\html\
copy _locback\private\*.html	_private_content\

echo Fixing array name in JS
fart ZDO.CHSite\wwwroot\dev-js\strings.de.js uiStringsEn uiStringsDe
fart ZDO.CHSite\wwwroot\dev-js\strings.hu.js uiStringsEn uiStringsHu
