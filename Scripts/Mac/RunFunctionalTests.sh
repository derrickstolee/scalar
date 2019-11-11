#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
elif
  shift # Consume the argument
fi

TEST_ON_PATH=1
if [ "$2" == "--local" ]; then
  TEST_ON_PATH=0
  shift # Consume the argument
fi

if [ $TEST_ON_PATH ]; then

  echo **************************
  echo Testing Scalar on the PATH
  echo **************************
  echo \$PATH:
  echo $PATH
  echo Scalar:
  which scalar
  echo Scalar.Service:
  which scalar.service
  echo Git:
  which git

  # Run the tests!
  dotnet run $SCALAR_SRCDIR/Scalar.FunctionalTests --configuration $CONFIGURATION -- --full-suite "$@"

else

  SCALAR_EXEC_PATH=$SCALAR_OUTPUTDIR/Scalar/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish/scalar
  SERVICE_EXEC_PATH=$SCALAR_OUTPUTDIR/Scalar.Service/bin/$CONFIGURATION/netcoreapp3.0/osx-x64/publish/scalar.service

  echo ********************************
  echo Testing Scalar from build output
  echo ********************************
  echo Scalar: $SCALAR_EXEC_PATH
  echo Scalar.Service: $SERVICE_EXEC_PATH

  # Run the tests!
  dotnet run $SCALAR_SRCDIR/Scalar.FunctionalTests --configuration $CONFIGURATION -- --scalar="$SCALAR_EXEC_PATH" --service="$SERVICE_EXEC_PATH" --full-suite "$@"

fi
