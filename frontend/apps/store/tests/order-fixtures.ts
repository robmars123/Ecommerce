import type { OrderDetail, OrderListPage, OrderSummary } from '@/features/orders/types';

export function makeOrderSummary(overrides: Partial<OrderSummary> = {}): OrderSummary {
  return {
    id: 'o-1',
    orderNumber: 'ORD-2026-000001',
    createdAt: '2026-03-04T10:00:00Z',
    status: 'Paid',
    grandTotal: { amount: '57.50', currency: 'USD' },
    trackingUrl: null,
    ...overrides,
  };
}

export function makeOrderPage(items: OrderSummary[], overrides: Partial<OrderListPage> = {}): OrderListPage {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0, ...overrides };
}

export function makeOrderDetail(overrides: Partial<OrderDetail> = {}): OrderDetail {
  return {
    id: 'o-1',
    orderNumber: 'ORD-2026-000001',
    createdAt: '2026-03-04T10:00:00Z',
    status: 'Shipped',
    items: [
      {
        variantId: 'v-1',
        sku: 'TEE-BLK-M',
        name: 'Black tee, M',
        unitPrice: { amount: '20.00', currency: 'USD' },
        quantity: 2,
        lineTotal: { amount: '40.00', currency: 'USD' },
        image: null,
        colour: null,
      },
    ],
    subtotal: { amount: '40.00', currency: 'USD' },
    discountTotal: { amount: '0.00', currency: 'USD' },
    shippingTotal: { amount: '5.00', currency: 'USD' },
    taxTotal: { amount: '0.00', currency: 'USD' },
    grandTotal: { amount: '45.00', currency: 'USD' },
    shippingMethod: { code: 'standard', name: 'Standard shipping' },
    shippingAddress: {
      fullName: 'Test Buyer',
      line1: '1 Example Street',
      line2: null,
      city: 'Exampleville',
      region: null,
      postalCode: '12345',
      country: 'US',
    },
    timeline: [
      {
        fromStatus: null,
        toStatus: 'PendingPayment',
        at: '2026-03-04T10:00:00Z',
        reason: null,
        description: 'We received your order and held your items for payment.',
      },
      {
        fromStatus: 'PendingPayment',
        toStatus: 'Paid',
        at: '2026-03-04T10:05:00Z',
        reason: null,
        description: 'Your payment was confirmed.',
      },
      {
        fromStatus: 'Paid',
        toStatus: 'Processing',
        at: '2026-03-05T09:00:00Z',
        reason: null,
        description: 'We started preparing your order.',
      },
      {
        fromStatus: 'Processing',
        toStatus: 'Shipped',
        at: '2026-03-06T09:00:00Z',
        reason: null,
        description: 'Your order was handed to the carrier.',
      },
    ],
    shipments: [
      {
        carrierName: 'Example Post',
        trackingNumber: 'AB 12/3',
        trackingUrl: 'https://tracking.example.com/AB%2012%2F3',
        shippedAt: '2026-03-06T09:00:00Z',
        deliveredAt: null,
      },
    ],
    paymentStatus: 'Succeeded',
    refunds: [],
    statusNote: null,
    payment: null,
    ...overrides,
  };
}
