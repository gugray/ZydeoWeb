IF EXIST chdict.tar.gz DEL chdict.tar.gz
IF EXIST chdict.tar DEL wchdict.tar
CD _deploy
7z a ..\chdict.tar .\**
CD ..
7z a chdict.tar.gz chdict.tar
DEL chdict.tar
