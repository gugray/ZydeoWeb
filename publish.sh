#!/bin/sh

rm -rf ./_deploy_chsite
dotnet publish ./ZDO.CHSite -c Release -o ./_deploy_chsite
