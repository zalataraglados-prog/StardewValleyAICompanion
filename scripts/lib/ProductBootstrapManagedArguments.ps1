function Get-ProductBootstrapManagedLoopArguments {
    [CmdletBinding()]
    param(
        [switch] $UseManagedCropTeacher,
        [switch] $JointDailyCalendar,
        [switch] $LegacyCropResume
    )
    if ($UseManagedCropTeacher -and $LegacyCropResume) {
        throw 'Managed bootstrap cannot fork a legacy unmanaged resume; recover its original owner through its existing entry.'
    }
    $arguments = @()
    if ($UseManagedCropTeacher) {
        $arguments += @('--use-managed-crop-teacher', '--emit-queue-execution-receipt', '--max-queue-item-attempts', '12')
    }
    if ($JointDailyCalendar) { $arguments += '--joint-daily-calendar' }
    return $arguments
}
