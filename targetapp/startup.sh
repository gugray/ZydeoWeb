#!/bin/bash

sphinxconf=/app/zhhu.conf
sphinxindexdir=/etc/zdo/zhhu-corpus/index
sphinxlogir=/etc/zdo/zhhu-corpus/log

if [ ! -d "$sphinxlogir" ]; then
    mkdir -p "$sphinxlogir"
fi

if [ ! -d "$sphinxindexdir" ]; then
    mkdir -p "$sphinxindexdir"
    indexer -c $sphinxconf zh
    indexer -c $sphinxconf hulo
    indexer -c $sphinxconf hustem
    indexer -c $sphinxconf stemdict
fi

searchd -c $sphinxconf

cd /app/chsite
dotnet ZDO.CHSite.dll
