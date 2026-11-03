import { describeError } from '../../api/client'
import { useBranchPickups, useBranchReturns } from '../../api/hooks'
import { BookingTable } from '../../components/staff/BookingTable'
import { StaffLayout } from '../../components/staff/StaffLayout'
import { Button, Inline, Input, Stack } from '../../components/ui'
import { navigate } from '../../lib/router'
import { useStaffBranch, useStaffDate } from '../../lib/staffPrefs'

/** The counter's day: who is collecting and who is bringing a car back, at the selected branch. */
export function TodayPage() {
  const [branchId] = useStaffBranch()
  const [date, setDate] = useStaffDate()
  const pickups = useBranchPickups(branchId, date)
  const returns = useBranchReturns(branchId, date)

  return (
    <StaffLayout page="today" title="Today at the counter" subtitle="Pickups and returns for the selected branch and day (SAST)." actions={<DatePicker value={date} onChange={setDate} />}>
      <Stack gap={6}>
        <BookingTable
          title="Pickups"
          icon="key"
          when="pickup"
          waitingFor={branchId ? undefined : "Choose your branch at the top to see its bookings."}
          rows={pickups.data}
          loading={pickups.isLoading}
          error={pickups.isError ? describeError(pickups.error) : null}
          onRetry={() => pickups.refetch()}
          empty="No pickups at this branch on this day."
          action={(b) => b.status === 'Confirmed'
            ? <Inline gap={2} justify="end"><Button size="sm" icon="key" onClick={() => navigate(`/agent/checkout/${b.reservationId}`)}>Check out</Button><Button variant="ghost" size="sm" onClick={() => navigate(`/agent/find?ref=${b.reference}`)}>More</Button></Inline>
            : <Button variant="ghost" size="sm" onClick={() => navigate(`/agent/find?ref=${b.reference}`)}>View</Button>}
        />
        <BookingTable
          title="Returns due"
          icon="assignment_return"
          when="return"
          waitingFor={branchId ? undefined : "Choose your branch at the top to see its bookings."}
          rows={returns.data}
          loading={returns.isLoading}
          error={returns.isError ? describeError(returns.error) : null}
          onRetry={() => returns.refetch()}
          empty="No cars due back at this branch by the end of this day."
          action={(b) => <Button variant={b.rental?.isLate ? 'primary' : 'secondary'} size="sm" icon="assignment_return" onClick={() => navigate(`/agent/returns/${b.reservationId}`)}>Take return</Button>}
        />
      </Stack>
    </StaffLayout>
  )
}

export function DatePicker({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <Input type="date" controlSize="sm" leadingIcon="calendar_today" fit aria-label="Day" value={value} onChange={(e) => e.target.value && onChange(e.target.value)} />
  )
}
