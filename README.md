# BimTools.Align

**BIM TOOLS – Tag Align Suite for Autodesk Revit 2020**

Professional IndependentTag alignment, arrange, and leader tools under the **BIM TOOLS** ribbon tab.

## Requirements

- Autodesk Revit **2020** (x64)
- .NET Framework 4.7.2
- Visual Studio 2019/2022

## Build

1. Open `BimTools.Align.sln`
2. Confirm API paths in `BimTools.Align.csproj` (default):

```xml
C:\Program Files\Autodesk\Revit 2020\RevitAPI.dll
C:\Program Files\Autodesk\Revit 2020\RevitAPIUI.dll
```

3. Build **Release | x64**
4. Output: `bin\x64\Release\BimTools.Align.dll`

## Install

Copy to:

```
%APPDATA%\Autodesk\Revit\Addins\2020\
    BimTools.Align.dll
    BimTools.Align.addin
    Resources\Icons\   (all PNG icons)
```

Restart Revit. Ribbon: **BIM TOOLS → ALIGN / ARRANGE / TAG TOOLS**

## Tools

### ALIGN
Left, Center, Right, Top, Middle, Bottom, Dist H, Dist V, To Line

### ARRANGE
Stack (uncross), Left Col, Right Col, Untangle, Flip, Parallel, Eq Offset, Match Len, Minimize

### TAG TOOLS
Horiz, Vert, To Ref, Straighten, Orient, Sel Cross, Pin, Unpin, Settings, About

## License

Internal BIM TOOLS project.
