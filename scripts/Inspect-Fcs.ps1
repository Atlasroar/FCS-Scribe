param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory,
    [string[]]$Targets = @('ToDo:SaveData', 'ToDo:AddItem', 'baseForm:saveFile')
)
$ErrorActionPreference = 'Stop'
$file = Join-Path (Resolve-Path -LiteralPath $FcsDirectory).Path 'forgotten construction set.exe'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = New-Object Reflection.AssemblyName($eventArgs.Name)
    $dependency = Join-Path $FcsDirectory ($name.Name + '.dll')
    if (Test-Path -LiteralPath $dependency) {
        return [Reflection.Assembly]::ReflectionOnlyLoadFrom($dependency)
    }
    return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($file)
    "Assembly: $($assembly.FullName)"
    "File version: $((Get-Item -LiteralPath $file).VersionInfo.FileVersion)"
    "SHA256: $((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash)"
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
    $opcodes = @{}
    [Reflection.Emit.OpCodes].GetFields() | Where-Object { $_.FieldType -eq [Reflection.Emit.OpCode] } |
        ForEach-Object { $op = $_.GetValue($null); $opcodes[([int]$op.Value -band 65535)] = $op }
    foreach ($target in $Targets) {
        $typeName, $methodName = $target.Split(':', 2)
        $type = $assembly.GetType('forgotten_construction_set.' + $typeName, $true)
        $methods = if ($methodName -eq '.ctor') { $type.GetConstructors($flags) } else {
            $type.GetMethods($flags) | Where-Object { $_.Name -eq $methodName }
        }
        foreach ($method in $methods) {
            "=== $typeName : $method"
            $bytes = $method.GetMethodBody().GetILAsByteArray()
            $i = 0
            while ($i -lt $bytes.Length) {
                $offset = $i
                $code = [int]$bytes[$i++]
                if ($code -eq 254) { $code = 65024 + [int]$bytes[$i++] }
                $op = $opcodes[$code]
                $operand = ''
                switch ($op.OperandType.ToString()) {
                    'InlineNone' {}
                    'ShortInlineI' { $operand = $bytes[$i]; $i++ }
                    'ShortInlineVar' { $operand = $bytes[$i]; $i++ }
                    'InlineVar' { $operand = [BitConverter]::ToUInt16($bytes, $i); $i += 2 }
                    'ShortInlineBrTarget' {
                        $operand = [int][sbyte]::Parse($bytes[$i].ToString('X2'),
                            [Globalization.NumberStyles]::HexNumber) + $i + 1
                        $i++
                    }
                    'InlineBrTarget' { $operand = [BitConverter]::ToInt32($bytes, $i) + $i + 4; $i += 4 }
                    'InlineI' { $operand = [BitConverter]::ToInt32($bytes, $i); $i += 4 }
                    'InlineI8' { $operand = [BitConverter]::ToInt64($bytes, $i); $i += 8 }
                    'ShortInlineR' { $operand = [BitConverter]::ToSingle($bytes, $i); $i += 4 }
                    'InlineR' { $operand = [BitConverter]::ToDouble($bytes, $i); $i += 8 }
                    'InlineSwitch' {
                        $count = [BitConverter]::ToInt32($bytes, $i)
                        $operand = "switch $count"
                        $i += 4 + 4 * $count
                    }
                    default {
                        $token = [BitConverter]::ToInt32($bytes, $i)
                        $i += 4
                        $operand = if ($op.OperandType.ToString() -eq 'InlineString') {
                            '"' + $assembly.ManifestModule.ResolveString($token) + '"'
                        } else { $assembly.ManifestModule.ResolveMember($token).ToString() }
                    }
                }
                '{0:X4}: {1} {2}' -f $offset, $op.Name, $operand
            }
        }
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}
