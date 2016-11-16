@echo off

echo Deleting old files
if exist .\_localization rmdir /s /q _localization
mkdir .\_localization


echo Copying content
copy ZDO.CHSite\files\strings\strings.en.json		.\_localization\strings.json
copy ZDO.CHSite\wwwroot\dev-js\strings.en.js		.\_localization\strings.js
copy ZDO.CHSite\files\html\*.en.html			.\_localization\
