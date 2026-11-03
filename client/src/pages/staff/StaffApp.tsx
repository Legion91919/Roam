import { useAccount } from '../../api/hooks'
import { StaffShellSkeleton } from '../../components/staff/StaffLayout'
import { useAuth } from '../../lib/auth'
import type { StaffPage } from '../../lib/router'
import { AddCustomerPage } from './AddCustomerPage'
import { CheckoutPage } from './CheckoutPage'
import { FindBookingPage } from './FindBookingPage'
import { AddVehiclePage, FleetPage, VehiclePage } from './FleetPages'
import { ReturnsPage } from './ReturnsPage'
import { StaffSignInPage } from './StaffSignInPage'
import { TodayPage } from './TodayPage'

/**
 * The "Roam Staff" app under #/agent/*. Only a signed-in user whose token carries the Agent role
 * gets past the sign-in page; customers who open a staff URL are sent there and signed out.
 * (This is UX: the API enforces the role on every staff endpoint.)
 */
export function StaffApp({ page, id, reference }: { page: StaffPage; id?: string; reference?: string }) {
  const { session } = useAuth()
  const account = useAccount(Boolean(session) && page !== 'signin')

  if (page === 'signin' || !session) return <StaffSignInPage />
  if (account.isLoading) {
    return <StaffShellSkeleton />
  }
  if (!account.data?.roles.includes('Agent')) return <StaffSignInPage notStaff />

  switch (page) {
    case 'checkout': return <CheckoutPage id={id} />
    case 'returns': return <ReturnsPage id={id} />
    case 'find': return <FindBookingPage key={reference} reference={reference ?? ''} />
    case 'customer': return <AddCustomerPage />
    case 'fleet': return <FleetPage />
    case 'vehicleNew': return <AddVehiclePage />
    case 'vehicle': return <VehiclePage key={id} id={id!} />
    default: return <TodayPage />
  }
}
