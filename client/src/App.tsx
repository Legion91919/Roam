import { Footer, TopBar } from './components/booking/Layout'
import { AuthProvider } from './lib/auth'
import { DraftProvider } from './lib/draft'
import { useRoute } from './lib/router'
import { MyBookingsPage } from './pages/MyBookingsPage'
import { ProfilePage } from './pages/ProfilePage'
import { ReservationPage } from './pages/ReservationPage'
import { ResultsPage } from './pages/ResultsPage'
import { ReviewPage } from './pages/ReviewPage'
import { SearchPage } from './pages/SearchPage'
import { SignInPage } from './pages/SignInPage'
import { StaffApp } from './pages/staff/StaffApp'

/**
 * components from components/ui (design-system primitives) and components/booking (feature parts).
 */
export default function App() {
  const route = useRoute()
  if (route.name === 'agent') {
    // Staff area: its own shell. The customer TopBar and Footer are not rendered here.
    return (
      <AuthProvider>
        <StaffApp page={route.page} id={route.id} reference={route.reference} />
      </AuthProvider>
    )
  }
  return (
    <AuthProvider>
      <DraftProvider>
        <div className="flex min-h-screen flex-col">
          <TopBar />
          {route.name === 'search' && <SearchPage />}
          {route.name === 'results' && <ResultsPage />}
          {route.name === 'review' && <ReviewPage />}
          {route.name === 'reservation' && <ReservationPage key={route.id} id={route.id} />}
          {route.name === 'bookings' && <MyBookingsPage />}
          {route.name === 'signin' && <SignInPage next={route.next} />}
          {route.name === 'profile' && <ProfilePage next={route.next} />}
          <Footer />
        </div>
      </DraftProvider>
    </AuthProvider>
  )
}
