BeforeDiscovery {
    $script:OnLinux = $IsLinux
    $script:IsRoot = if ($IsLinux) {
        (Get-Content /proc/self/status | Select-String '(?m)^Uid:\s+(\d+)').Matches.Groups[1].Value -eq '0'
    } else { $false }
    $script:hasDBus = if ($IsLinux) {
        [System.IO.File]::Exists('/run/dbus/system_bus_socket')
    } else { $false }
}

BeforeAll {
    $script:ModuleDir = Join-Path $PSScriptRoot '..' 'src' 'Services.Linux.Native' 'bin' 'Release' 'net8.0'
    $script:ModulePath = Join-Path $script:ModuleDir 'Services.Linux.Native.dll'
    $script:PublishDir  = Join-Path $script:ModuleDir 'publish'
    if (Test-Path (Join-Path $script:PublishDir 'Services.Linux.Native.dll')) {
        $script:ModuleDir  = $script:PublishDir
        $script:ModulePath = Join-Path $script:PublishDir 'Services.Linux.Native.dll'
    }
    if (-not (Get-Module Services.Linux.Native -EA SilentlyContinue)) {
        Import-Module $script:ModulePath -Force
    }
}

AfterAll {
    Remove-Module Services.Linux.Native -Force -EA SilentlyContinue
}

Describe 'Module surface' -Skip:(-not $script:OnLinux) {
    It 'exports exactly 9 cmdlets' {
        $cmds = Get-Command -Module Services.Linux.Native
        $cmds.Count | Should -Be 9
    }

    It 'exports Get-Service' {
        Get-Command Get-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Start-Service' {
        Get-Command Start-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Stop-Service' {
        Get-Command Stop-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Restart-Service' {
        Get-Command Restart-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Set-Service' {
        Get-Command Set-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports New-Service' {
        Get-Command New-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Remove-Service' {
        Get-Command Remove-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Suspend-Service' {
        Get-Command Suspend-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }

    It 'exports Resume-Service' {
        Get-Command Resume-Service -Module Services.Linux.Native | Should -Not -BeNullOrEmpty
    }
}

Describe 'Output types' -Skip:(-not ($script:OnLinux -and $script:hasDBus)) {
    It 'Get-Service returns LinuxServiceInfo objects' {
        $svc = Get-Service -Name ssh* | Select-Object -First 1
        $svc | Should -Not -BeNullOrEmpty
        $svc.Name | Should -BeOfType [string]
        $svc.Status | Should -BeOfType [string]
    }
}

Describe 'Suspend-Service and Resume-Service are stubs' -Skip:(-not ($script:OnLinux -and $script:hasDBus)) {
    It 'Suspend-Service writes a PlatformNotSupported error' {
        $err = $null
        Suspend-Service -Name sshd -ErrorVariable err -EA SilentlyContinue
        $err[0].FullyQualifiedErrorId | Should -Match 'PlatformNotSupported'
    }

    It 'Resume-Service writes a PlatformNotSupported error' {
        $err = $null
        Resume-Service -Name sshd -ErrorVariable err -EA SilentlyContinue
        $err[0].FullyQualifiedErrorId | Should -Match 'PlatformNotSupported'
    }
}

Describe 'Get-Service' -Skip:(-not ($script:OnLinux -and $script:hasDBus)) {
    It 'returns services without parameters' {
        $svcs = Get-Service
        $svcs | Should -Not -BeNullOrEmpty
    }

    It 'filters by name with wildcard' {
        $svcs = Get-Service -Name s*
        $svcs | Should -Not -BeNullOrEmpty
        foreach ($s in $svcs) { $s.Name -match '^s' | Should -BeTrue }
    }

    It 'filters by exact name' {
        $svc = Get-Service -Name sshd
        $svc | Should -Not -BeNullOrEmpty
    }


}

Describe 'Start/Stop/Restart-Service -WhatIf' -Skip:(-not ($script:OnLinux -and $script:hasDBus)) {
    It 'Start-Service -WhatIf does not throw' {
        { Start-Service -Name sshd -WhatIf } | Should -Not -Throw
    }

    It 'Stop-Service -WhatIf does not throw' {
        { Stop-Service -Name sshd -WhatIf } | Should -Not -Throw
    }

    It 'Restart-Service -WhatIf does not throw' {
        { Restart-Service -Name sshd -WhatIf } | Should -Not -Throw
    }

    It 'Set-Service -WhatIf does not throw' {
        { Set-Service -Name sshd -StartupType Manual -WhatIf } | Should -Not -Throw
    }
}

Describe 'New-Service and Remove-Service -WhatIf' -Skip:(-not ($script:OnLinux -and $script:IsRoot)) {
    It 'New-Service -WhatIf does not throw' {
        { New-Service -Name pester-test -BinaryPathName '/usr/bin/true' -WhatIf } | Should -Not -Throw
    }

    It 'Remove-Service -WhatIf does not throw' {
        { Remove-Service -Name pester-test -WhatIf } | Should -Not -Throw
    }
}

Describe 'Module loads on Windows' -Skip:$script:OnLinux {
    It 'imports without error' {
        { Import-Module $script:ModulePath -Force -EA Stop } | Should -Not -Throw
    }
}
