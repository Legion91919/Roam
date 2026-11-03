// Types shared by every API area.

export type VehicleCategory = 'Economy' | 'Compact' | 'Suv' | 'Luxury'

/** One page of a list endpoint (GET /api/agent/vehicles). */
export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}

/** RFC 7807 body produced by the API's ApiExceptionHandler (and ASP.NET Core Identity's ValidationProblem). */
export interface ProblemDetails {
  status: number
  title: string
  detail?: string
  code?: string
  errors?: Record<string, string[]>
  /** payment.declined only: the gateway's reason, e.g. "insufficient_funds". */
  reason?: string
}
