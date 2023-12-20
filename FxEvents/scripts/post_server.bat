echo Current working directory is: %cd%
xcopy /Y /E "..\CompiledLibs\Server\Debug\netstandard2.0\FxEvents.Server.dll" "L:\FiveM\FreeRoamProject\resources\[local]\frp\Server"
xcopy /Y /E "..\CompiledLibs\Server\Debug\netstandard2.0\FxEvents.Server.pdb" "L:\FiveM\FreeRoamProject\resources\[local]\frp\Server"
echo Done!
