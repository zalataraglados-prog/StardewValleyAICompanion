function Get-ProductBootstrapManagedLoopArguments {
    [CmdletBinding()]
    param(
        [switch] $UseManagedCropTeacher,
        [switch] $JointDailyCalendar,
        [switch] $LegacyCropResume,
        [switch] $ContinueGlobalDailyPlanAfterCropTeacherRun
    )
    if ($UseManagedCropTeacher -and $LegacyCropResume) {
        throw 'Managed bootstrap cannot fork a legacy unmanaged resume; recover its original owner through its existing entry.'
    }
    if ($ContinueGlobalDailyPlanAfterCropTeacherRun -and -not $UseManagedCropTeacher) {
        throw 'ContinueGlobalDailyPlanAfterCropTeacherRun requires UseManagedCropTeacher and its original native owner boundary.'
    }
    $arguments = @()
    if ($UseManagedCropTeacher) {
        $arguments += @('--use-managed-crop-teacher', '--emit-queue-execution-receipt', '--max-queue-item-attempts', '12')
    }
    if ($JointDailyCalendar) { $arguments += '--joint-daily-calendar' }
    if ($ContinueGlobalDailyPlanAfterCropTeacherRun) { $arguments += '--continue-global-daily-plan-after-crop-teacher-run' }
    return $arguments
}
