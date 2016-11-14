SET MUTATION=HDD
dotnet ZDO.CHSite\bin\Debug\netcoreapp1.0\ZDO.CHSite.dll --task recreate-db
dotnet ZDO.CHSite\bin\Debug\netcoreapp1.0\ZDO.CHSite.dll --task import-freq ..\_dev_site_data\subtlex-ch.txt
dotnet ZDO.CHSite\bin\Debug\netcoreapp1.0\ZDO.CHSite.dll --task import-dict ..\_dev_site_data\handedict.txt ..\_dev_site_data


