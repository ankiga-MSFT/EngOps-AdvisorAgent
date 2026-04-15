param PWafPolicyName string
param PafdWafPolicySku  string

resource AFDPolicy 'Microsoft.Network/frontdoorwebapplicationfirewallpolicies@2024-02-01' = {
  name: PWafPolicyName
  location: 'Global'
  sku: {
    name: PafdWafPolicySku 
  }
  properties: {
    policySettings: {
      enabledState: 'Enabled'
      mode: 'Detection'
      requestBodyCheck: 'Enabled'
      javascriptChallengeExpirationInMinutes: 30
    }
    customRules: {
      rules: [
        {
          name: 'RateLimitRule'
          enabledState: 'Enabled'
          priority: 99
          ruleType: 'RateLimitRule'
          rateLimitDurationInMinutes: 5
          rateLimitThreshold: 800
          matchConditions: [
            {
              matchVariable: 'SocketAddr'
              operator: 'IPMatch'
              negateCondition: false
              matchValue: [
                '0.0.0.0/0'
                '::/0'
              ]
              transforms: []
            }
            {
              matchVariable: 'RequestMethod'
              operator: 'Equal'
              negateCondition: true
              matchValue: [
                'OPTIONS'
              ]
              transforms: []
            }
          ]
          action: 'Block'
          groupBy: [
            {
              variableName: 'SocketAddr'
            }
          ]
        }
      ]
    }
    managedRules: {
      managedRuleSets: [
        {
          ruleSetType: 'Microsoft_DefaultRuleSet'
          ruleSetVersion: '2.1'
          ruleSetAction: 'Block'
          ruleGroupOverrides: []
          exclusions: []
        }
        {
          ruleSetType: 'Microsoft_BotManagerRuleSet'
          ruleSetVersion: '1.0'
          ruleGroupOverrides: []
          exclusions: []
        }
      ]
    }
  }

 
}
output AFDPolicyId string = AFDPolicy.id

