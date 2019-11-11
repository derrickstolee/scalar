#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/../InitializeEnvironment.sh"

CONFIGURATION=$1
SCALAR_STAGEDIR=$2
if [ -z $SCALAR_STAGEDIR ] || [ -z $CONFIGURATION ]; then
  echo 'ERROR: Usage: CreateBuildDrop.sh [configuration] [build drop root directory]'
  exit 1
fi

# Set up the build drop directory structure
rm -rf $SCALAR_STAGEDIR || exit 1
mkdir -p $SCALAR_STAGEDIR || exit 1

# Publish tests to the build drop
dotnet publish $SCALAR_SRCDIR/Scalar.FunctionalTests --configuration $CONFIGURATION --runtime osx-x64 --output $SCALAR_STAGEDIR
