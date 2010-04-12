#!/bin/sh -x

rm -rf smoke-test
git tfs clone http://team:8080 $/sandbox smoke-test
cd smoke-test
git tfs fetch
echo ok > testfile
git add testfile
git commit -m "Test commit"
git tfs shelve TEST_SHELVESET
git tfs shelve TEST_SHELVESET
git tfs shelve -f TEST_SHELVESET
"/c/Program Files (x86)/Microsoft Visual Studio 9.0/Common7/IDE/TF.exe" shelvesets -format:detailed TEST_SHELVESET
