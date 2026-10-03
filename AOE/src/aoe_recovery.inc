// The recipe persists only disk-derived RVAs, operands and exact fingerprints.
bool RecoverAndSaveAoeProfile(const std::filesystem::path& image,const std::filesystem::path& directory,BuildProfile& profile,std::filesystem::path& selected,std::string& error){
    try{
        AoeLocatorResult located;if(!LocateAoeImage(image,located,error))return false;
        Pe pe(image);if(Sha256(pe.data.data(),pe.data.size())!=located.image.sha256)throw std::runtime_error("AOE image changed during recovery");
        auto site=[&](const char* name){auto*s=Find(located,name);if(!s||!s->valid)throw std::runtime_error(std::string("Missing AOE evidence: ")+name);return s->rva;};
        auto& r=located.recovered;
        r.initialCallRva=site("AoeLifecycleCaller");r.initialPrepRva=r.initialCallRva-located.prepDistance;r.initialReturnRva=r.initialCallRva+5;
        r.sharedWorkerRva=site("SharedOperation020A");r.producerRva=site("OperationProducer");r.producerCallRva=site("ProducerCall");r.typedActorLookupRva=site("TypedActorLookup");
        r.targetBuilderVectorRva=site("TargetVectorBuilder");r.differentialParsedRva=site("ParsedActionBoundary");r.idSerializationRva=site("RecordSerialization")+14;r.typeSerializationRva=site("RecordSerialization")+24;
        const auto typed=r.typedActorLookupRva;
        r.initialPrepBytes=pe.bytes(r.initialPrepRva,size_t(located.prepDistance));r.initialCallBytes=pe.bytes(r.initialCallRva,5);r.initialReturnBytes=pe.bytes(r.initialReturnRva,1);
        r.sharedWorkerBytes=pe.bytes(r.sharedWorkerRva,15);r.producerBytes=pe.bytes(r.producerRva,16);r.producerCallBytes=pe.bytes(r.producerCallRva,5);r.typedActorLookupBytes=pe.bytes(typed,38);
        r.targetBuilderVectorBytes=pe.bytes(r.targetBuilderVectorRva,15);r.differentialParsedBytes=pe.bytes(r.differentialParsedRva,6);r.idSerializationBytes=pe.bytes(r.idSerializationRva,10);r.typeSerializationBytes=pe.bytes(r.typeSerializationRva,11);
        r.actorPositionOffset=0xB0;r.targetInspectorAvailable=false;r.initialNxAvailable=true;r.liveValidationRequired=true;r.aoeLocatorRequired=true;
        if(located.visualReady){r.visualSuppressionAvailable=true;r.visualReaderRva=located.visualReader.rva;r.visualLookupRva=located.visualReader.lookupRva;r.visualFieldOffset=located.visualReader.fieldOffset;r.visualBaseRegister=located.visualReader.baseRegister;r.visualReaderBytes=located.visualReader.fingerprint;}
        profile={};profile.schemaVersion=3;profile.targetSha256=located.image.sha256;profile.created=UtcNow();profile.notes="Automatically recovered disk semantics; same-session live cast validation required.";profile.runtime=r;
        for(auto [name,value]:std::initializer_list<std::pair<const char*,uint64_t>>{{"initialPrepRva",r.initialPrepRva},{"initialCallRva",r.initialCallRva},{"initialReturnRva",r.initialReturnRva},{"sharedWorkerRva",r.sharedWorkerRva},{"producerRva",r.producerRva},{"producerCallRva",r.producerCallRva},{"typedActorLookupRva",typed},{"targetBuilderRva",pe.function(r.targetBuilderVectorRva)},{"targetBuilderVectorRva",r.targetBuilderVectorRva},{"differentialParsedRva",r.differentialParsedRva},{"idSerializationRva",r.idSerializationRva},{"typeSerializationRva",r.typeSerializationRva},{"templateLookupRva",site("TemplateLookup")}})profile.anchors.push_back({name,value});
        for(const auto& signature:located.signatures)if(signature.valid&&signature.name!="VisualSfxReader")profile.anchors.push_back({"locator_"+signature.name,signature.rva});
        if(!ValidateAoeProfile(profile,located,error))return false;
        std::filesystem::create_directories(directory);selected=directory/(profile.targetSha256+".json");auto temp=selected;temp+=L".tmp";
        std::ofstream out(temp,std::ios::binary|std::ios::trunc);if(!out)throw std::runtime_error("Cannot save AOE recovered profile");
        out<<"{\n\"schemaVersion\":3,\n\"targetSha256\":\""<<profile.targetSha256<<"\",\n\"created\":\""<<profile.created<<"\",\n\"notes\":\"Automatic semantic recovery; live validation required\",\n";
        for(const auto& a:profile.anchors)out<<'"'<<a.name<<"\":\""<<Hex(a.rva)<<"\",\n";
        auto number=[&](const char* name,uint64_t v){out<<'"'<<name<<"\":\""<<Hex(v)<<"\",\n";};
        for(auto [name,v]:std::initializer_list<std::pair<const char*,uint64_t>>{{"targetVectorOffset",r.targetVectorOffset},{"actorIdOffset",r.actorIdOffset},{"actorTypeOffset",r.actorTypeOffset},{"actorEligibilityOffset",r.actorEligibilityOffset},{"actorStatusOffset",r.actorStatusOffset},{"localActorOffset",r.localActorOffset},{"actorPositionOffset",r.actorPositionOffset},{"targetRecordBytes",r.targetRecordBytes},{"targetMaximumEntries",r.targetMaximumEntries}})number(name,v);
        for(auto [type,name]:std::initializer_list<std::pair<unsigned,const char*>>{{1,"type1TreeOffset"},{2,"type2TreeOffset"},{7,"type7TreeOffset"},{9,"type9TreeOffset"},{10,"type10TreeOffset"},{11,"type11TreeOffset"},{17,"type17TreeOffset"},{19,"type19TreeOffset"}})number(name,r.actorTreeOffsets[type]);
        auto bytes=[&](const char* name,const std::vector<uint8_t>& value){out<<'"'<<name<<"\":\""<<BytesHex(value)<<"\",\n";};
        for(auto [name,v]:std::initializer_list<std::pair<const char*,std::vector<uint8_t>>>{{"initialPrepExpectedBytes",r.initialPrepBytes},{"initialCallExpectedBytes",r.initialCallBytes},{"initialReturnExpectedBytes",r.initialReturnBytes},{"sharedWorkerExpectedBytes",r.sharedWorkerBytes},{"producerExpectedBytes",r.producerBytes},{"producerCallExpectedBytes",r.producerCallBytes},{"typedActorLookupExpectedBytes",r.typedActorLookupBytes},{"targetBuilderVectorExpectedBytes",r.targetBuilderVectorBytes},{"differentialParsedExpectedBytes",r.differentialParsedBytes},{"idSerializationExpectedBytes",r.idSerializationBytes},{"typeSerializationExpectedBytes",r.typeSerializationBytes}})bytes(name,v);
        if(r.visualSuppressionAvailable){number("visualReaderRva",r.visualReaderRva);number("visualLookupRva",r.visualLookupRva);number("visualFieldOffset",r.visualFieldOffset);bytes("visualReaderExpectedBytes",r.visualReaderBytes);out<<"\"visualBaseRegister\":\""<<X64RegisterName(r.visualBaseRegister)<<"\",\n";}
        out<<"\"targetInspectorAvailable\":false,\"initialNxAvailable\":true,\"liveValidationRequired\":true,\"aoeLocatorRequired\":true,\"visualSuppressionAvailable\":"<<(r.visualSuppressionAvailable?"true":"false")<<"\n}\n";out.flush();if(!out)throw std::runtime_error("AOE profile write failed");out.close();
        if(!MoveFileExW(temp.c_str(),selected.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH))throw std::runtime_error("AOE profile replacement failed");error.clear();return true;
    }catch(const std::exception& e){profile={};error=e.what();return false;}
}
