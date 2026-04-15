param PfailDeployment string 
var pdeploy=bool(PfailDeployment)

var validation = pdeploy ? fail('SDP Team Failed Deployment by passing PfailDeployment parameter value to true') : 'Validation passed'

output result string = validation
