#!/bin/sh

[ -e chsite.tar.gz ] && rm chsite.tar.gz
rm -rf ./_deploy_chsite
dotnet publish ./ZDO.CHSite -c Release -o ./_deploy_chsite
cd _deploy_chsite
tar -czf ../chsite.tar.gz .
cd ..
