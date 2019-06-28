#!/bin/bash

if [[ -z "$1" ]] ; then
	echo "Usage: build.sh version_id"
	exit 1
fi

if [[ -z "$UNITY_CMD" ]] ; then
	echo "Point \$UNITY_CMD to the Unity editor binary."
	exit 1
fi

build() {
	CURRENT_DIR=`pwd`
	PROJECT_DIR=$(dirname $CURRENT_DIR)
        NAME="$1-$2-$3"
	ZIP_NAME="$NAME.zip"
	rm $ZIP_NAME
	$UNITY_CMD -batchmode -quit -logFile -projectPath $PROJECT_DIR -executeMethod BuildUtils.BuildCommandline +name $1 +target $2 +version $3 +scene $4 +devel $5
	cp -r resources/$2/* $NAME
	zip -r $ZIP_NAME $NAME
}

rm -rf StandaloneRenderer-*-$1*
build StandaloneRenderer Darwin-x86_64 $1 Assets/Scenes/StandaloneRenderer.unity false
build StandaloneRenderer Linux-x86_64 $1 Assets/Scenes/StandaloneRenderer.unity false
build StandaloneRenderer Darwin-x86_64 $1-devel Assets/Scenes/StandaloneRenderer.unity true
build StandaloneRenderer Linux-x86_64 $1-devel Assets/Scenes/StandaloneRenderer.unity true

zip -r builds-$1.zip StandaloneRenderer-*-$1 StandaloneRenderer-*-$1-devel

