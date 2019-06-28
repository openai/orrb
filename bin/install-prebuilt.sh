#!/bin/bash
wget http://openai-public.s3.amazonaws.com/github.com/openai/orrb/builds/builds-20190514.zip
unzip builds-20190514.zip
rm builds-201905014.zip

echo
echo
echo "  Execute or add this to your .bashrc/.bashprofile."
echo "  export ORRB_BINARIES_DIR=\""`pwd`"\""
echo
echo
