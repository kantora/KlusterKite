# KlusterKite.Data

A bundle of generic actors and other abstractions to handle basic data work (mainly CRUD)

In every project, there is alway exists some data that is stored in the DB but is not under heavy load. But, nevertheless, there is need in some administration (or other) UI that handles this data.

## [`BaseCrudActor<TContext>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_base_crud_actor.html)

There is a ready abstraction to handle basic CRUD operation with data. Just make a subclass of `BaseCrudActor` and you can make operations with simple messages:
* [`CrudActionMessage<TData, TId>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_c_r_u_d_1_1_action_messages_1_1_crud_action_message.html) will perform CRUD operation with single data entity `TData` with and id of type `TId`. 
    Then it will send back the [`CrudActionResponse<TData>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_c_r_u_d_1_1_action_messages_1_1_crud_action_response.html) message.
    
    In order to do this operations from the box the [`DataFactory<TContext, TObject, TId>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_data_factory.html) should be implemented and registered with the DI.
* [`CollectionRequest<TObject>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_c_r_u_d_1_1_action_messages_1_1_collection_request.html) to request the list of entities. The actor will reply with the [`CollectionResponse<TObject>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_c_r_u_d_1_1_action_messages_1_1_collection_response.html) message.

Both messages can contain [`ApiRequest`](../Docs/Doxygen/html/class_kluster_kite_1_1_a_p_i_1_1_client_1_1_api_request.html) subtree, that should be parsed on order to load related data.

## KlusterKite.Data.EF

Very often EntityFramework.Core is used to work with DBMS. For this case there is a ready solution to work with data contexts and data factories.
[`EntityDataFactory<TContext, TObject, TId>`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_e_f_1_1_entity_data_factory.html) is ready to work with EF contexts and DbSets. It can parse the `ApiRequest` to include the related data. 
As for now, it is strongly recommended to use completely sync version of the factory (`EntityDataFactorySync<TContext, TObject, TId>`) due to performance issues in EF. As soon as it will be fixed by MS, this recommendation will be removed.

To acquire context the [`UniversalContextFactory`](../Docs/Doxygen/html/class_kluster_kite_1_1_data_1_1_e_f_1_1_universal_context_factory.html) is already registered in the DI by [`KlusterKite.Data.EF`] plugin. All you need is to add the EF provider plugin and specify provider name and connection properties.
At this moment the are several DBMS drivers specified:
* `KlusterKite.Data.EF.Npgsql` to work with Postgres
* `KlusterKite.Data.EF.InMemory` to work with mock database for test purposes
