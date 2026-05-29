import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    baseline: {
      executor: 'constant-arrival-rate',
      rate: 20,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 10,
      maxVUs: 20,
    },
  },
  thresholds: {
    http_req_duration: ['p(50)<5000', 'p(99)<10000'],
  },
};

export default function () {
  const res = http.get('http://localhost:5051/api/author-report');
  check(res, { 'status 200': (r) => r.status === 200 });
}
