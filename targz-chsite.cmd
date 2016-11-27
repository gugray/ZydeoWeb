CD %~dp0

IF EXIST chsite.tar.gz DEL chsite.tar.gz
IF EXIST chsite.tar DEL chsite.tar
CD _deploy_chsite
7z a ..\chsite.tar .\**
CD ..
7z a chsite.tar.gz chsite.tar
DEL chsite.tar
