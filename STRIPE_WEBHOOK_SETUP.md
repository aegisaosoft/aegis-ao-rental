# Stripe Webhook Setup Guide

This guide explains how to configure Stripe webhooks to receive payment events and keep your application in sync with Stripe.

## What Are Webhooks?

Webhooks are automated messages sent from Stripe to your application when events occur (e.g., payment succeeded, charge refunded). They ensure your database stays synchronized with Stripe's payment status.

## Events Handled

The webhook endpoint handles these Stripe events:

- ✅ **payment_intent.succeeded** - Payment authorization successful
- ✅ **payment_intent.payment_failed** - Payment failed
- ✅ **charge.succeeded** - Charge completed successfully
- ✅ **charge.failed** - Charge failed
- ✅ **charge.refunded** - Charge refunded
- ✅ **checkout.session.completed** - Checkout session completed
- ⚠️ **Other events** - Logged but not processed

## Setup Instructions

### 1. Configure Backend (appsettings.json)

Add the Stripe webhook secret to your `appsettings.json`:

```json
{
  "Stripe": {
    "SecretKey": "sk_test_xxxxxxxxxxxxx",
    "PublishableKey": "pk_test_xxxxxxxxxxxxx",
    "WebhookSecret": "whsec_xxxxxxxxxxxxx"
  }
}
```

**Note:** The `WebhookSecret` is optional but **highly recommended** for production. It verifies that webhooks are actually coming from Stripe.

### 2. Configure Stripe Dashboard

#### For Development (Local Testing):

1. Install Stripe CLI:
   ```bash
   # Windows
   scoop install stripe
   
   # Mac
   brew install stripe/stripe-cli/stripe
   ```

2. Login to Stripe CLI:
   ```bash
   stripe login
   ```

3. Forward webhooks to your local server:
   ```bash
   stripe listen --forward-to http://localhost:5000/api/webhooks/stripe
   ```

4. The CLI will display your webhook signing secret (starts with `whsec_`). Add it to your `appsettings.json`.

#### For Production:

1. Go to [Stripe Dashboard > Webhooks](https://dashboard.stripe.com/webhooks)

2. Click **"Add endpoint"**

3. Configure the endpoint:
   - **Endpoint URL:** `https://your-domain.com/api/webhooks/stripe`
   - **Events to send:** Select these events:
     - `payment_intent.succeeded`
     - `payment_intent.payment_failed`
     - `charge.succeeded`
     - `charge.failed`
     - `charge.refunded`
     - `checkout.session.completed`

4. Click **"Add endpoint"**

5. Copy the **Signing secret** (starts with `whsec_`)

6. Add it to your production `appsettings.json` or environment variables:
   ```bash
   # Azure App Service Configuration
   az webapp config appsettings set \
     --name your-app-name \
     --resource-group your-resource-group \
     --settings Stripe__WebhookSecret=whsec_xxxxxxxxxxxxx
   ```

### 3. Test the Webhook

#### Using Stripe CLI:

```bash
# Trigger a test payment_intent.succeeded event
stripe trigger payment_intent.succeeded
```

#### Using Stripe Dashboard:

1. Go to **Webhooks > Your endpoint**
2. Click **"Send test webhook"**
3. Select an event type
4. Click **"Send test webhook"**

### 4. Monitor Webhook Activity

#### Check Logs:

**Backend API Logs:**
```bash
# Look for these log entries:
Stripe webhook received: payment_intent.succeeded [evt_xxxxx]
Payment succeeded: pi_xxxxx, Amount: 10500
Updated payment status for booking xxx-xxx-xxx
```

**Stripe Dashboard:**
1. Go to **Webhooks > Your endpoint**
2. View recent deliveries
3. Check response status (should be `200 OK`)

## Webhook Endpoint Details

### URL
```
POST http://localhost:5000/api/webhooks/stripe     # Development
POST https://your-domain.com/api/webhooks/stripe   # Production
```

### Headers
- `Content-Type: application/json`
- `Stripe-Signature: t=timestamp,v1=signature` (for verification)

### Response
```json
{
  "received": true
}
```

## Security

### Webhook Signature Verification

The webhook endpoint verifies that requests are actually from Stripe using the `Stripe-Signature` header:

1. **Without Secret:** All webhooks are accepted (⚠️ not recommended for production)
2. **With Secret:** Only webhooks with valid signatures are processed (✅ recommended)

### Best Practices

1. ✅ **Always use webhook secrets in production**
2. ✅ **Monitor webhook delivery success rate**
3. ✅ **Handle duplicate events** (Stripe may send the same event multiple times)
4. ✅ **Return 2xx status quickly** (process asynchronously if needed)
5. ✅ **Log all webhook events** for debugging

## What Happens When Webhook Receives Events

### payment_intent.succeeded
1. Finds payment in database by `StripePaymentIntentId`
2. Updates payment status to `"succeeded"`
3. Sets `ProcessedAt` timestamp
4. Saves changes to database

### charge.refunded
1. Finds payment by `StripeChargeId` or `StripePaymentIntentId`
2. Updates payment status to `"refunded"`
3. Sets `RefundAmount` and `RefundDate`
4. Saves changes to database

### checkout.session.completed
1. Finds payment by `PaymentIntentId`
2. Updates payment status to `"succeeded"`
3. Optionally sends confirmation email

## Troubleshooting

### Webhooks Returning 404

**Problem:** Stripe webhooks return `404 Not Found`

**Solution:**
1. Verify the webhook endpoint is registered in `index.js`
2. Restart your Node.js server
3. Check the URL in Stripe Dashboard matches your endpoint

### Webhooks Failing Signature Verification

**Problem:** Webhooks return `400 Bad Request - Invalid signature`

**Solution:**
1. Verify the webhook secret in `appsettings.json` matches Stripe Dashboard
2. Make sure you're using the correct secret for test/live mode
3. Check that the `Stripe-Signature` header is being forwarded correctly

### Payment Status Not Updating

**Problem:** Payment status stays as "Pending" after successful payment

**Solution:**
1. Check backend API logs for webhook processing
2. Verify payment has `StripePaymentIntentId` in database
3. Confirm webhook is being received (check Stripe Dashboard)
4. Check for database connection issues

### Duplicate Events

**Problem:** Same webhook event received multiple times

**Solution:**
This is normal! Stripe may send the same event multiple times. The webhook handler is idempotent - processing the same event twice won't cause issues.

## Environment Variables

### Development (.env)
```
STRIPE_SECRET_KEY=sk_test_xxxxxxxxxxxxx
STRIPE_PUBLISHABLE_KEY=pk_test_xxxxxxxxxxxxx
STRIPE_WEBHOOK_SECRET=whsec_xxxxxxxxxxxxx
```

### Production (Azure App Service)
```bash
az webapp config appsettings set \
  --name your-app-name \
  --resource-group your-resource-group \
  --settings \
    Stripe__SecretKey=sk_live_xxxxxxxxxxxxx \
    Stripe__PublishableKey=pk_live_xxxxxxxxxxxxx \
    Stripe__WebhookSecret=whsec_xxxxxxxxxxxxx
```

## Testing Checklist

- [ ] Webhook endpoint responds with 200 OK
- [ ] Signature verification works (if webhook secret configured)
- [ ] `payment_intent.succeeded` updates payment status
- [ ] `charge.refunded` updates refund information
- [ ] Backend logs show webhook events being processed
- [ ] Stripe Dashboard shows successful deliveries
- [ ] Payment status updates appear in Admin Dashboard

## Common Webhook Events Flow

### Successful Payment:
```
1. checkout.session.completed
   ↓
2. payment_intent.created
   ↓
3. payment_intent.succeeded
   ↓
4. charge.succeeded
```

### Refund:
```
1. charge.refunded
   ↓
   Payment status updated to "refunded"
```

## Additional Resources

- [Stripe Webhooks Documentation](https://stripe.com/docs/webhooks)
- [Stripe CLI Documentation](https://stripe.com/docs/stripe-cli)
- [Testing Webhooks](https://stripe.com/docs/webhooks/test)
- [Webhook Best Practices](https://stripe.com/docs/webhooks/best-practices)

## Support

If webhooks are not working:
1. Check this guide's Troubleshooting section
2. Review backend API logs
3. Check Stripe Dashboard webhook delivery logs
4. Verify endpoint URL is correct
5. Test with Stripe CLI locally

