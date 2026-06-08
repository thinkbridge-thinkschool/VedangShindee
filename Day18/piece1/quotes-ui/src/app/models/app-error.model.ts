export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: { [field: string]: string[] };
}

export interface AppError {
  status: number;
  friendlyMessage: string;
  raw?: ProblemDetails;
}
