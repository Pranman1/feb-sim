#!/usr/bin/env bash
# Compile the FEB scripts against the editor's assemblies without opening Unity (no licence needed).
# Upstream classes that drag in HDRP/plugins are stubbed with their real public fields.
set -euo pipefail
cd "$(dirname "$0")"
E="${UNITY_DATA:-$HOME/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Data}"
UGUI="$E/Resources/PackageManager/BuiltInPackages/com.unity.ugui"
OUT=$(mktemp -d)
python3 - > "$OUT/stubs.cs" <<'PY'
import re, pathlib
def fields(name):
    src = pathlib.Path(f"Assets/Scripts/{name}.cs").read_text()
    lines = []
    for m in re.finditer(r'^\s*public\s+([\w\.\[\]<>]+)\s+(\w+(?:\s*,\s*\w+)*)\s*(?:=[^;]*)?;', src, re.M):
        if m.group(1) not in ('class', 'enum', 'void', 'static', 'override', 'virtual', 'const'):
            lines.append(f"    public {m.group(1)} {m.group(2)};")
    return "\n".join(lines)
print("using UnityEngine;\nusing UnityEngine.UI;")
print("namespace TMPro { public class TextMeshProUGUI : MonoBehaviour { public string text; } }")
for cls in ("Socket", "ResetManager"):
    print(f"public class {cls} : MonoBehaviour {{\n{fields(cls)}\n}}")
for cls in ("CoSimManager", "WeatherManager", "TimeOfDay", "TwistController",
            "VehicleLighting", "CarLighting", "ROVLighting", "LIDAR3D", "TireFriction", "TLController"):
    print(f"public class {cls} : MonoBehaviour {{ }}")
PY
REFS=$(ls "$E"/Managed/UnityEngine/*.dll | grep -vE "\.pdb|Cecil|il2cpp|Test|Analytics|Purchasing|Cloud|Collab" | sed 's/^/-r:/')
NETSTD=$(ls "$E"/NetStandard/ref/2.1.0/*.dll | sed 's/^/-r:/')
"$E/NetCoreRuntime/dotnet" "$E/DotNetSdkRoslyn/csc.dll" -nologo -target:library -out:"$OUT/feb.dll" \
  -nowarn:CS0618,CS0649,CS0414,CS0108,CS0109,CS0162,CS0168,CS0169,CS0219 \
  -define:UNITY_EDITOR -define:UNITY_2022_3 -define:UNITY_STANDALONE $NETSTD $REFS \
  $(find "$UGUI/Runtime" -name "*.cs" -not -name AssemblyInfo.cs) "$OUT/stubs.cs" \
  Assets/FEB/Scripts/*.cs Assets/FEB/Editor/*.cs \
  Assets/Scripts/{LapTimer,FollowTarget,CLIManager,SocketConnection,DrivingMode,CameraSwitch,SceneLighting,LIDAR,VehicleController,AutomobileController,AbstractTargetFollower,WheelEffects,WheelEncoder,GPS,IMU,FrameGrabber}.cs \
  > "$OUT/csc.log" 2>&1 || { grep -E "error" "$OUT/csc.log"; exit 1; }
echo "FEB scripts compile"
rm -rf "$OUT"
