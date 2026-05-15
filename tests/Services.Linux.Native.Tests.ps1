BeforeDiscovery {
    $script:OnLinux = $IsLinux
    $script:IsRoot = if ($IsLinux) {
        (Get-Content /proc/self/status | Select-String '(?m)^Uid:\s+(\d+)').Matches.Groups[1].Value -eq '0'
    }
    else { $false }
    $script:hasDBus = if ($IsLinux) {
        [System.IO.File]::Exists('/run/dbus/system_bus_socket')
    }
    else { $false }
}

BeforeAll {
    $script:ModuleDir = Join-Path $PSScriptRoot '..' 'src' 'Services.Linux.Native' 'bin' 'Release' 'net8.0'
    $script:ModulePath = Join-Path $script:ModuleDir 'Services.Linux.Native.dll'
    $script:PublishDir = Join-Path $script:ModuleDir 'publish'
    if (Test-Path (Join-Path $script:PublishDir 'Services.Linux.Native.dll')) {
        $script:ModuleDir = $script:PublishDir
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

Describe 'Suspend-Service and Resume-Service are stubs' -Skip:(-not $script:OnLinux) {
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

Describe 'Start/Stop/Restart-Service -WhatIf' -Skip:(-not $script:OnLinux) {
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

Describe 'Elevation errors' -Skip:($script:OnLinux -and $script:IsRoot) {
    It 'Start-Service writes a meaningful error when not root' {
        $err = @()
        Start-Service -Name 'sshd' -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'Start-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.StartServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }

    It 'Stop-Service writes a meaningful error when not root' {
        $err = @()
        Stop-Service -Name 'sshd' -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'Stop-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.StopServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }

    It 'Restart-Service writes a meaningful error when not root' {
        $err = @()
        Restart-Service -Name 'sshd' -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'Restart-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.RestartServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }

    It 'Set-Service writes a meaningful error when not root' {
        $err = @()
        Set-Service -Name 'sshd' -StartupType Manual -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'Set-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.SetServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }

    It 'New-Service writes a meaningful error when not root' {
        $err = @()
        New-Service -Name 'pester-test-elev' -BinaryPathName '/usr/bin/true' -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'New-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.NewServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }

    It 'Remove-Service writes a meaningful error when not root' {
        $err = @()
        Remove-Service -Name 'pester-test-elev' -ErrorVariable err -ErrorAction SilentlyContinue
        $err.Count | Should -BeGreaterThan 0
        $err[0].Exception.Message | Should -Be 'Remove-Service requires root privileges.'
        $err[0].FullyQualifiedErrorId | Should -Be 'ElevationRequired,Microsoft.PowerShell.Commands.RemoveServiceCommand'
        $err[0].CategoryInfo.Category | Should -Be 'PermissionDenied'
    }
}

Describe 'Module loads on Windows' -Skip:$script:OnLinux {
    It 'imports without error' {
        { Import-Module $script:ModulePath -Force -EA Stop } | Should -Not -Throw
    }
}
