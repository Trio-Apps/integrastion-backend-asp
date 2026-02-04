#!/bin/bash

if [[ ! -d certs ]]
then
    mkdir certs
    cd certs/
    if [[ ! -f localhost.pfx ]]
    then
        dotnet dev-certs https -v -ep localhost.pfx -p 2a4a868c-4b36-471c-9c89-05e98c2f5b79 -t
    fi
    cd ../
fi

docker-compose up -d
