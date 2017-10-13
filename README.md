# BnS-Tools

## Contents
- BnSDat library (with a "little" edit for more extensive) written in C# (Original work from [RedDot-3ND7355](https://github.com/RedDot-3ND7355/BnS-Buddy) and [LokiReborn](http://www.bladeandsouldojo.com/profile/4070-lokireborn/) and [ronny1982](https://sourceforge.net/projects/bns-tools/files/bnsdat/))
- BnSDat-Automation: a extract/compress tool similiar to [ronny1982's bnsdat tool](https://sourceforge.net/projects/bns-tools/files/bnsdat/) with an addition command...that is `patching`. This tool uses the library above.
- BnSLaunchWrapper: This is a proxy .exe to launch the real client's binary file with additional parameters like `-USEALLCORES` and swapping .xml file and some other convenient functions.

## Credits:
- [RedDot-3ND7355](https://github.com/RedDot-3ND7355/BnS-Buddy)
- [LokiReborn](http://www.bladeandsouldojo.com/profile/4070-lokireborn/)
- [ronny1982](https://sourceforge.net/projects/bns-tools/files/bnsdat/)

## BnSDat-Automation Usage:
*Actually you can just run the binary .exe file directly to get the MessageBox that show the string below:*

Usage: BnSDat-Automation.exe \<command\> [param1] [param2] [param...]

* Patching: command is "-p"

  * [Param1] is where the original xml.dat

  * [Param2] is where the modded xml.dat will be created.

  * [Param3] is where the patching information ([`.xmlpatch` format](https://github.com/Leayal/BnS-Tools/wiki#xmlpatch-file-format)) files located.

  * [Param4] (Optional, Recommended) is the path of temporary folder. If this ommited, it will use memory.

* Extracting: command is "-e"

  * [Param1] is where the target xml.dat

  * [Param2] is where the extracted files at.

* Compressing: command is "-c"

  * [Param1] is the compressed xml.dat file will be.

  * [Param2] is the folder which will be compressed.

  * [Param3] (Optional, Recommended) is the path of temporary folder. If this ommited, it will use memory.
