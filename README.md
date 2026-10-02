I don't like working with Python.

So, I decided to make a scuffed RBXGSDecomp ObjDiff Config Generator with C#.
It uses the delink program made by HaydnTrigg.

This program tries to detect if the directory you inputed has the WebService.dll and WebService.pdb.
Then, it will (hopefully) try and download the delink program for your OS.
After that, it will delink the PDB and generate its object files.

Another reason why I made this program is because I don't want to manually export object files with Ghidra.
Of course, there are some bugs but it works for the most part.

I tested it on Windows, I don't know if it works on macOS or Linux.
