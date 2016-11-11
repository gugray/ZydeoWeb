IF EXIST console.tar.gz DEL console.tar.gz
IF EXIST console.tar DEL console.tar
CD _deploy_console
7z a ..\console.tar .\**
CD ..
7z a console.tar.gz console.tar
DEL console.tar
