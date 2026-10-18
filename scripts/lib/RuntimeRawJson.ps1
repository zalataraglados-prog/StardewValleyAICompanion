function Get-RuntimeJsonStringEnd {
    param(
        [Parameter(Mandatory)] [string] $RawJson,
        [Parameter(Mandatory)] [int] $StartIndex
    )
    if ($StartIndex -lt 0 -or $StartIndex -ge $RawJson.Length -or
        $RawJson[$StartIndex] -ne '"') {
        throw "JSON string must start with a quote."
    }

    $index = $StartIndex + 1
    while ($index -lt $RawJson.Length) {
        $character = $RawJson[$index]
        if ($character -eq '"') {
            return $index + 1
        }
        if ($character -eq '\') {
            $index += 2
            if ($index -gt $RawJson.Length) {
                throw "JSON string ended inside an escape sequence."
            }
            continue
        }
        if ([int]$character -lt 0x20) {
            throw "JSON string contains an unescaped control character."
        }
        $index++
    }
    throw "JSON string is not terminated."
}

function Get-RuntimeJsonValueEnd {
    param(
        [Parameter(Mandatory)] [string] $RawJson,
        [Parameter(Mandatory)] [int] $StartIndex
    )
    if ($StartIndex -lt 0 -or $StartIndex -ge $RawJson.Length) {
        throw "JSON value is missing."
    }

    $first = $RawJson[$StartIndex]
    if ($first -eq '"') {
        return Get-RuntimeJsonStringEnd -RawJson $RawJson `
            -StartIndex $StartIndex
    }
    if ($first -eq '{' -or $first -eq '[') {
        $closers = [Collections.Generic.Stack[char]]::new()
        $index = $StartIndex
        while ($index -lt $RawJson.Length) {
            $character = $RawJson[$index]
            if ($character -eq '"') {
                $index = Get-RuntimeJsonStringEnd -RawJson $RawJson `
                    -StartIndex $index
                continue
            }
            if ($character -eq '{') {
                $closers.Push('}')
            }
            elseif ($character -eq '[') {
                $closers.Push(']')
            }
            elseif ($character -eq '}' -or $character -eq ']') {
                if ($closers.Count -eq 0 -or
                    $closers.Peek() -ne $character) {
                    throw "JSON value has mismatched delimiters."
                }
                $closers.Pop() | Out-Null
                if ($closers.Count -eq 0) {
                    return $index + 1
                }
            }
            $index++
        }
        throw "JSON object or array is not terminated."
    }

    $index = $StartIndex
    while ($index -lt $RawJson.Length) {
        $character = $RawJson[$index]
        if ([char]::IsWhiteSpace($character) -or
            $character -eq ',' -or $character -eq '}' -or
            $character -eq ']') {
            break
        }
        $index++
    }
    if ($index -eq $StartIndex) {
        throw "JSON primitive value is missing."
    }
    return $index
}

function Get-RuntimeRawJsonProperty {
    param(
        [Parameter(Mandatory)] [string] $RawJson,
        [Parameter(Mandatory)] [string] $PropertyName
    )
    if ([string]::IsNullOrWhiteSpace($PropertyName)) {
        throw "JSON property name is required."
    }

    try {
        $parsedForValidation = ConvertFrom-Json -InputObject $RawJson `
            -ErrorAction Stop
    }
    catch {
        throw "JSON document is invalid: $($_.Exception.Message)"
    }
    if ($null -eq $parsedForValidation -or
        $RawJson.TrimStart()[0] -ne '{') {
        throw "JSON root must be an object."
    }

    $index = 0
    while ($index -lt $RawJson.Length -and
        [char]::IsWhiteSpace($RawJson[$index])) { $index++ }
    if ($index -ge $RawJson.Length -or $RawJson[$index] -ne '{') {
        throw "JSON root must be an object."
    }
    $index++
    $matchedValue = $null
    $matchCount = 0

    while ($true) {
        while ($index -lt $RawJson.Length -and
            [char]::IsWhiteSpace($RawJson[$index])) { $index++ }
        if ($index -ge $RawJson.Length) {
            throw "JSON root object is not terminated."
        }
        if ($RawJson[$index] -eq '}') {
            $index++
            break
        }

        $nameStart = $index
        $nameEnd = Get-RuntimeJsonStringEnd -RawJson $RawJson `
            -StartIndex $nameStart
        $nameToken = $RawJson.Substring($nameStart, $nameEnd - $nameStart)
        try {
            $decodedName = ConvertFrom-Json -InputObject $nameToken `
                -ErrorAction Stop
        }
        catch {
            throw "JSON property name is invalid: $($_.Exception.Message)"
        }

        $index = $nameEnd
        while ($index -lt $RawJson.Length -and
            [char]::IsWhiteSpace($RawJson[$index])) { $index++ }
        if ($index -ge $RawJson.Length -or $RawJson[$index] -ne ':') {
            throw "JSON property is missing its colon."
        }
        $index++
        while ($index -lt $RawJson.Length -and
            [char]::IsWhiteSpace($RawJson[$index])) { $index++ }

        $valueStart = $index
        $valueEnd = Get-RuntimeJsonValueEnd -RawJson $RawJson `
            -StartIndex $valueStart
        if ([string]::Equals(
                [string]$decodedName,
                $PropertyName,
                [StringComparison]::Ordinal)) {
            $matchCount++
            if ($matchCount -gt 1) {
                throw "JSON property is duplicated: $PropertyName"
            }
            $matchedValue = $RawJson.Substring(
                $valueStart,
                $valueEnd - $valueStart)
        }

        $index = $valueEnd
        while ($index -lt $RawJson.Length -and
            [char]::IsWhiteSpace($RawJson[$index])) { $index++ }
        if ($index -ge $RawJson.Length) {
            throw "JSON root object is not terminated."
        }
        if ($RawJson[$index] -eq ',') {
            $index++
            continue
        }
        if ($RawJson[$index] -eq '}') {
            $index++
            break
        }
        throw "JSON property has an invalid trailing delimiter."
    }

    while ($index -lt $RawJson.Length -and
        [char]::IsWhiteSpace($RawJson[$index])) { $index++ }
    if ($index -ne $RawJson.Length) {
        throw "JSON document has trailing content."
    }
    if ($matchCount -ne 1) {
        throw "JSON property is missing: $PropertyName"
    }
    return $matchedValue
}
