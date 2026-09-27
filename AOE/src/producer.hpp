#pragma once
#include "tracer.hpp"
#include <ostream>
namespace aoe {
inline constexpr char ProducerLimitation[]="A +99240 execution identifies the typed message producer but does not by itself prove damage, target selection, or AOE mechanics.";
void DecodeProducerArguments(Event& event);
void SnapshotProducer(HANDLE process,Event& event);
bool ValidateProducerVector(ProducerSnapshot& snapshot);
std::string ArgumentText(const ProducerArgument& arg);
std::vector<FieldStability> CompareProducerFields(const Capture& capture);
void WriteProducerJson(std::ostream& out,const Event& event);
void WriteProducerFieldsJson(std::ostream& out,const std::vector<FieldStability>& fields);
std::string ProducerDetails(const Event& event);
std::string ProducerFieldDetails(const std::vector<FieldStability>& fields);
Capture SyntheticProducerCapture();
}
