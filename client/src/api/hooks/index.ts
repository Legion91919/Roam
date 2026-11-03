// -----------------------------------------------------------------------------------------------
// TanStack Query hooks: the client-side mirror of CQRS, and the ONLY place components reach the API.
//   useQuery    -> API queries  (GET, cached, refetched, deduplicated)
//   useMutation -> API commands (POST/PUT, then invalidate the queries the change affects)
// One file per API area; import from '../api/hooks'.
// -----------------------------------------------------------------------------------------------
export * from './booking'
export * from './customer'
export * from './fleet'
export * from './inspections'
export * from './payments'
export { queryKeys } from './queryKeys'
export * from './staff'
