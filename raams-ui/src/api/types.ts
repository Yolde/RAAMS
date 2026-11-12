export interface Me {
  userId: string
  email: string
  displayName: string
  function: string
  department: string
  roles: string[]
  permissions: string[]
  tokenExpiresAt: string
}

export interface PaginatedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface PagedFilter {
  page?: number
  pageSize?: number
  sortBy?: string
  sortDirection?: string
}

export interface AssessmentFilter extends PagedFilter {
  status?: string
  function?: string
  quarter?: string
  year?: number
  leadAuditorId?: string
}

export interface Assessment {
  id: string
  referenceNumber: string
  name: string
  scope: string
  function: string
  quarter: string
  year: number
  leadAuditorId: string
  leadAuditorName: string
  coordinatorId: string
  coordinatorName: string
  status: string
  completionDate: string | null
  cancellationReason: string | null
  createdDate: string
  modifiedDate: string
}

export interface CreateAssessment {
  referenceNumber: string
  name: string
  scope: string
  function: string
  quarter: string
  year: number
  leadAuditorId: string
  coordinatorId: string
}

export interface ActionFilter extends PagedFilter {
  status?: string
  actionPartyId?: string
  auditeeId?: string
  findingId?: string
  overdueOnly?: boolean
}

export interface Action {
  id: string
  findingId: string
  findingReference: string
  referenceNumber: string
  actionDescription: string
  actionRequired: string
  actionPartyId: string
  actionPartyName: string
  auditeeId: string
  auditeeName: string
  targetDate: string | null
  actionTaken: string | null
  status: string | null
  submittedDate: string | null
  approvedDate: string | null
  closedDate: string | null
  createdDate: string
}

export interface SubmitAction {
  id: string
  actionTaken: string
}

export interface CommentAction {
  id: string
  comments?: string | null
}
