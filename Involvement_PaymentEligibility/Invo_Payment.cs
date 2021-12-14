using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using System.Net;
using Microsoft.Xrm.Sdk.Query;

namespace Involvement_PaymentEligibility
{
    public class Invo_Payment : IPlugin
    {


        Entity involvementEnt = new Entity("new_involvement");
        string validReqReason;


        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                Entity entity = (Entity)context.InputParameters["Target"];

                // Obtain the organization service reference which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                //If the record has been updated
                try
                {
                    if (context.MessageName == "Update")
                    {
                        if (entity.LogicalName == "new_involvement")
                        {

                            string postInvStatus = string.Empty;
                            string postFranchisee = string.Empty;
                            string postStipend = string.Empty;




                            if (context.PostEntityImages.Contains("invStatusImage") && context.PostEntityImages["invStatusImage"] is Entity)
                            {

                                Entity postMessageImage = (Entity)context.PostEntityImages["invStatusImage"];

                                postInvStatus = postMessageImage.Attributes["new_involvementstatus"].ToString();

                            }



                            if (context.PostEntityImages.Contains("FranchiseeImage") && context.PostEntityImages["FranchiseeImage"] is Entity)
                            {

                                Entity postMessageImage = (Entity)context.PostEntityImages["FranchiseeImage"];

                                postFranchisee = postMessageImage.GetAttributeValue<EntityReference>("new_franchiseeid").Id.ToString();

                            }



                            if (context.PostEntityImages.Contains("StipendImage") && context.PostEntityImages["StipendImage"] is Entity)
                            {

                                Entity postMessageImage = (Entity)context.PostEntityImages["StipendImage"];

                                postStipend = postMessageImage.GetAttributeValue<EntityReference>("new_stipendprogramme").Id.ToString();

                            }





                            involvementEnt.Id = entity.Id;


                            if (postInvStatus == "Candidate")
                            {
                                CheckInvolvementValidity(postFranchisee, postStipend, postInvStatus, service);
                            }

                        }


                    }

                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in the plugin. ", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("Tegra Plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }



        //CheckInvolvementValididty
        public void CheckInvolvementValidity(string franchisee, string stipend, string invStatus, IOrganizationService service)
        {
            try
            {

                string reqQuery = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
              <entity name='new_requirement'>
                <attribute name='new_requirementid' />
                <attribute name='new_name' />
                <attribute name='createdon' />
                <attribute name='new_documenttype' />
                <order attribute='new_name' descending='false' />
                <filter type='and'>
                  <condition attribute='new_requirementfrequency' operator='eq' value='100000001' />
                </filter>
                <link-entity name='new_stipendrequirement' from='new_requirementid' to='new_requirementid' alias='ah'>
                  <filter type='and'>
                    <condition attribute='new_stipendprogramme' operator='eq' value='" + WebUtility.HtmlEncode(stipend) + @"' />
                  </filter>
                </link-entity>
                <link-entity name='new_validationresult' from='new_requirementid' to='new_requirementid' link-type='outer' alias='ai'>
                  <attribute name='new_validity'/>
                  <attribute name='new_reason'/>
                  <filter type='and'>
                    <condition attribute='new_franchiseeid' operator='eq' value='" + WebUtility.HtmlEncode(franchisee) + @"' />
                  </filter>
                </link-entity>
              </entity>
            </fetch>";

                EntityCollection result = service.RetrieveMultiple(new FetchExpression(reqQuery));


                bool validReq = CheckRequirementValidity(result);


                if (validReq)
                {
                    if (invStatus == "Candidate" || invStatus == "Suspended")
                    {
                        //Changing involvement status to Participant
                        involvementEnt.Attributes["new_involvementstatus"] = new OptionSetValue(100000002);
                    }
                }

            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the plugin. ", ex);
            }

           

        }





        //CheckRequirementValidity
        public bool CheckRequirementValidity(EntityCollection reqResults)
        {
            bool validReq = true;
            validReqReason = "";

            try
            {
                foreach (var c in reqResults.Entities)
                {

                    if (c.GetAttributeValue<EntityReference>("new_validationresult").Id.ToString() == "")
                    {
                        validReqReason = /*"\n" +*/ c.Attributes["new_documenttype"].ToString() + " not submitted";

                        validReq = false;

                    }
                    else if (c.Attributes["new_validationresult.new_validity"].ToString() == "Pending")
                    {
                        validReqReason = /*"\n" +*/ c.Attributes["new_documenttype"].ToString() + " submitted, being checked";

                        validReq = false;

                    }
                    else if (c.Attributes["new_validationresult.new_validity"].ToString() != "Valid")
                    {

                        validReqReason = /*"\n" +*/ c.Attributes["new_validationresult.new_reason"].ToString();

                        validReq = false;

                    }

                }

                return validReq;

            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw new InvalidPluginExecutionException("An error occurred in the plugin. ", ex);
            }

            
        }





    }
}


